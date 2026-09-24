using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Domain.Rules;
using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.ViewModels.Applications;
using PropertyRentalSystem.Web.ViewModels.Shared;

namespace PropertyRentalSystem.Web.Controllers;

[Authorize(Roles = Roles.Applicant)]
public class ApplicationsController : ModalFormControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private string CurrentUserId => _userManager.GetUserId(User)!;

    // ---------- My applications ----------

    public async Task<IActionResult> Index()
    {
        var applications = await _db.RentalApplications
            .Where(a => a.ApplicantId == CurrentUserId)
            .OrderByDescending(a => a.Id)
            .Select(a => new ApplicationListItemViewModel
            {
                Id = a.Id,
                PropertyName = a.Unit.Property.Name,
                UnitNumber = a.Unit.UnitNumber,
                Status = a.Status
            })
            .ToListAsync();

        return View(applications);
    }

    // ---------- Browse available units ----------

    public async Task<IActionResult> Browse()
    {
        var today = DateTime.UtcNow.Date;

        // Narrow to not-yet-expired leases in SQL, then apply the exact "covers today" rule
        // in memory so the same LeaseRules.CoversDate logic is what gets unit-tested.
        var unavailableUnitIds = (await _db.Leases.Where(l => l.EndDate >= today).ToListAsync())
            .Where(l => LeaseRules.CoversDate(l.StartDate, l.EndDate, today))
            .Select(l => l.UnitId)
            .ToHashSet();

        var units = await _db.Units
            .Where(u => !unavailableUnitIds.Contains(u.Id))
            .OrderBy(u => u.Property.Name).ThenBy(u => u.UnitNumber)
            .Select(u => new BrowseUnitViewModel
            {
                Id = u.Id,
                PropertyName = u.Property.Name,
                PropertyAddress = u.Property.Address,
                UnitNumber = u.UnitNumber,
                Bedrooms = u.Bedrooms,
                MonthlyRent = u.MonthlyRent,
                UnitTypeName = u.UnitType.Name
            })
            .ToListAsync();

        var myOpenApplications = await _db.RentalApplications
            .Where(a => a.ApplicantId == CurrentUserId && RentalApplicationRules.OpenStatuses.Contains(a.Status))
            .ToDictionaryAsync(a => a.UnitId, a => a.Id);

        foreach (var unit in units)
        {
            if (myOpenApplications.TryGetValue(unit.Id, out var applicationId))
                unit.ExistingApplicationId = applicationId;
        }

        return View(units);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int unitId)
    {
        var unit = await _db.Units.FindAsync(unitId);
        if (unit == null) return NotFound();

        var today = DateTime.UtcNow.Date;
        if (await HasActiveLeaseAsync(unitId, today))
        {
            TempData["Error"] = "This unit is no longer available.";
            return RedirectToAction(nameof(Browse));
        }

        var existing = await _db.RentalApplications.FirstOrDefaultAsync(a =>
            a.ApplicantId == CurrentUserId && a.UnitId == unitId && RentalApplicationRules.OpenStatuses.Contains(a.Status));
        if (existing != null)
            return RedirectToAction(nameof(Wizard), new { id = existing.Id });

        var currentUser = await _userManager.GetUserAsync(User);
        var application = new RentalApplication
        {
            ApplicantId = CurrentUserId,
            UnitId = unitId,
            Status = ApplicationStatus.Draft,
            Email = currentUser?.Email ?? string.Empty
        };
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = ApplicationStatus.Draft,
            ChangedByUserId = CurrentUserId,
            ChangedAt = DateTime.UtcNow
        });

        _db.RentalApplications.Add(application);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Wizard), new { id = application.Id });
    }

    // ---------- The wizard ----------

    [HttpGet]
    public async Task<IActionResult> Wizard(int id)
    {
        var application = await LoadForCurrentUserAsync(id);
        if (application == null) return NotFound();

        var step = DetermineStep(application);
        return View(BuildViewModel(application, step));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Wizard(ApplicationWizardViewModel model, string action)
    {
        var application = await LoadForCurrentUserAsync(model.Id);
        if (application == null) return NotFound();

        // Controllers reject posts that are not allowed: only Draft/Returned may be edited or
        // submitted. Stale client state (browser back/forward cache, a second tab, a resubmitted
        // old page) shouldn't dead-end on a blank error — send the user back to the wizard,
        // which recomputes the real current step from the database.
        if (!RentalApplicationRules.IsEditable(application.Status))
            return RedirectToAction(nameof(Wizard), new { id = application.Id });

        switch (action)
        {
            case "Back":
                var previousStep = model.Step switch
                {
                    WizardStep.ResidenceHistory => WizardStep.ApplicantInfo,
                    WizardStep.Summary => WizardStep.ResidenceHistory,
                    _ => model.Step
                };
                // We're re-rendering a different step than what was posted — clear ModelState
                // so asp-for reads the fresh model (e.g. the new Step) instead of redisplaying
                // whatever was in the just-submitted form for those same field names.
                ModelState.Clear();
                return View(BuildViewModel(application, previousStep));

            case "Continue" when model.Step == WizardStep.ApplicantInfo:
                ValidateApplicantInfo(model);
                if (!ModelState.IsValid)
                    return View(BuildViewModel(application, WizardStep.ApplicantInfo, model));

                application.FullName = model.FullName.Trim();
                application.Phone = model.Phone.Trim();
                application.Email = model.Email.Trim();
                application.CurrentAddress = model.CurrentAddress.Trim();
                application.IsApplicantInfoComplete = true;
                await _db.SaveChangesAsync();

                ModelState.Clear();
                return View(BuildViewModel(application, WizardStep.ResidenceHistory));

            case "Continue" when model.Step == WizardStep.ResidenceHistory:
                application.IsResidenceHistoryComplete = true;
                await _db.SaveChangesAsync();

                ModelState.Clear();

                return View(BuildViewModel(application, WizardStep.Summary));

            case "Submit" when model.Step == WizardStep.Summary:
                if (!RentalApplicationRules.CanSubmit(application.IsApplicantInfoComplete, application.IsResidenceHistoryComplete))
                {
                    ModelState.AddModelError(string.Empty, "Complete both sections before submitting.");
                    return View(BuildViewModel(application, WizardStep.Summary));
                }

                if (await HasActiveLeaseAsync(application.UnitId, DateTime.UtcNow.Date))
                {
                    ModelState.AddModelError(string.Empty, "This unit currently has an active lease and can't accept new applications.");
                    return View(BuildViewModel(application, WizardStep.Summary));
                }

                application.Status = ApplicationStatus.Submitted;
                _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
                {
                    RentalApplicationId = application.Id,
                    Status = ApplicationStatus.Submitted,
                    ChangedByUserId = CurrentUserId,
                    ChangedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                TempData["Message"] = "Application submitted.";
                return RedirectToAction(nameof(Index));

            default:
                // action/Step didn't match any real transition (e.g. a resubmitted stale
                // page) — recover by showing the wizard at its actual current step.
                return RedirectToAction(nameof(Wizard), new { id = application.Id });
        }
    }

    // ---------- Residence history (added/edited/removed through a modal) ----------

    [HttpGet]
    public async Task<IActionResult> AddResidence(int applicationId)
    {
        var application = await GetOwnedEditableApplicationAsync(applicationId);
        if (application == null) return NotFound();

        return PartialView("_ResidenceForm", new ResidenceHistoryFormViewModel { RentalApplicationId = applicationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddResidence(ResidenceHistoryFormViewModel model)
    {
        var application = await GetOwnedEditableApplicationAsync(model.RentalApplicationId);
        if (application == null) return NotFound();

        if (!ModelState.IsValid)
            return PartialView("_ResidenceForm", model);

        _db.ResidenceHistories.Add(new ResidenceHistory
        {
            RentalApplicationId = model.RentalApplicationId,
            Address = model.Address,
            LandlordName = model.LandlordName,
            LandlordPhone = model.LandlordPhone,
            MoveInDate = model.MoveInDate,
            MoveOutDate = model.MoveOutDate
        });
        await _db.SaveChangesAsync();

        return FormSuccess();
    }

    [HttpGet]
    public async Task<IActionResult> EditResidence(int id)
    {
        var residence = await _db.ResidenceHistories.Include(r => r.RentalApplication).FirstOrDefaultAsync(r => r.Id == id);
        if (residence == null || !IsOwnedAndEditable(residence.RentalApplication)) return NotFound();

        return PartialView("_ResidenceForm", new ResidenceHistoryFormViewModel
        {
            Id = residence.Id,
            RentalApplicationId = residence.RentalApplicationId,
            Address = residence.Address,
            LandlordName = residence.LandlordName,
            LandlordPhone = residence.LandlordPhone,
            MoveInDate = residence.MoveInDate,
            MoveOutDate = residence.MoveOutDate
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditResidence(int id, ResidenceHistoryFormViewModel model)
    {
        if (id != model.Id) return BadRequest();

        var residence = await _db.ResidenceHistories.Include(r => r.RentalApplication).FirstOrDefaultAsync(r => r.Id == id);
        if (residence == null || !IsOwnedAndEditable(residence.RentalApplication)) return NotFound();

        if (!ModelState.IsValid)
            return PartialView("_ResidenceForm", model);

        residence.Address = model.Address;
        residence.LandlordName = model.LandlordName;
        residence.LandlordPhone = model.LandlordPhone;
        residence.MoveInDate = model.MoveInDate;
        residence.MoveOutDate = model.MoveOutDate;
        await _db.SaveChangesAsync();

        return FormSuccess();
    }

    [HttpGet]
    public async Task<IActionResult> DeleteResidenceConfirm(int id)
    {
        var residence = await _db.ResidenceHistories.Include(r => r.RentalApplication).FirstOrDefaultAsync(r => r.Id == id);
        if (residence == null || !IsOwnedAndEditable(residence.RentalApplication)) return NotFound();

        return PartialView("~/Views/Shared/_ConfirmDelete.cshtml", new ConfirmDeleteViewModel
        {
            Title = "Delete Residence",
            Message = $"Delete the residence at \"{residence.Address}\"?",
            ActionUrl = Url.Action(nameof(DeleteResidence), new { id })!,
            RefreshUrl = Url.Action(nameof(ResidenceHistorySection), new { applicationId = residence.RentalApplicationId })!,
            RefreshTarget = "#residence-history-section"
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteResidence(int id)
    {
        var residence = await _db.ResidenceHistories.Include(r => r.RentalApplication).FirstOrDefaultAsync(r => r.Id == id);
        if (residence == null || !IsOwnedAndEditable(residence.RentalApplication)) return NotFound();

        _db.ResidenceHistories.Remove(residence);
        await _db.SaveChangesAsync();

        return FormSuccess();
    }

    // Re-rendered into #residence-history-section by modal-forms.js after a residence add/edit/delete.
    [HttpGet]
    public async Task<IActionResult> ResidenceHistorySection(int applicationId)
    {
        var application = await LoadForCurrentUserAsync(applicationId);
        if (application == null) return NotFound();

        return PartialView("_ResidenceHistorySection", BuildViewModel(application, WizardStep.ResidenceHistory));
    }

    // ---------- Withdraw ----------

    [HttpGet]
    public async Task<IActionResult> WithdrawConfirm(int id)
    {
        var application = await LoadForCurrentUserAsync(id);
        if (application == null || !RentalApplicationRules.IsOpen(application.Status)) return NotFound();

        return PartialView("~/Views/Shared/_ConfirmDelete.cshtml", new ConfirmDeleteViewModel
        {
            Title = "Withdraw Application",
            Message = "Withdraw this application? This can't be undone.",
            ActionUrl = Url.Action(nameof(Withdraw), new { id })!
            // No RefreshUrl/RefreshTarget: the withdraw button appears on both the list and
            // the wizard page, so modal-forms.js falls back to a full reload after success.
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int id)
    {
        var application = await LoadForCurrentUserAsync(id);
        if (application == null || !RentalApplicationRules.IsOpen(application.Status)) return NotFound();

        application.Status = ApplicationStatus.Withdrawn;
        _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            RentalApplicationId = application.Id,
            Status = ApplicationStatus.Withdrawn,
            ChangedByUserId = CurrentUserId,
            ChangedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return FormSuccess();
    }

    // ---------- Helpers ----------

    private async Task<RentalApplication?> LoadForCurrentUserAsync(int id)
    {
        var application = await _db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.ResidenceHistories)
            .FirstOrDefaultAsync(a => a.Id == id);

        return application != null && application.ApplicantId == CurrentUserId ? application : null;
    }

    private async Task<RentalApplication?> GetOwnedEditableApplicationAsync(int id)
    {
        var application = await LoadForCurrentUserAsync(id);
        return application != null && IsOwnedAndEditable(application) ? application : null;
    }

    private bool IsOwnedAndEditable(RentalApplication application) =>
        application.ApplicantId == CurrentUserId && RentalApplicationRules.IsEditable(application.Status);

    private async Task<bool> HasActiveLeaseAsync(int unitId, DateTime asOf)
    {
        var unitLeases = await _db.Leases.Where(l => l.UnitId == unitId).ToListAsync();
        return unitLeases.Any(l => LeaseRules.CoversDate(l.StartDate, l.EndDate, asOf));
    }

    // Non-editable (Submitted/terminal) applications have nothing to walk through, so they
    // always resume at the read-only Summary, which shows both sections at once. Editable
    // applications resume at the first section that still needs work; if both are already
    // complete (a Draft paused right before submitting, or a Returned one coming back for a
    // fix), start over from the top rather than landing on Summary — that used to require
    // the applicant to notice and click Back before they could find anything editable.
    private static WizardStep DetermineStep(RentalApplication application)
    {
        if (!RentalApplicationRules.IsEditable(application.Status))
            return WizardStep.Summary;

        if (!application.IsApplicantInfoComplete) return WizardStep.ApplicantInfo;
        if (!application.IsResidenceHistoryComplete) return WizardStep.ResidenceHistory;
        return WizardStep.ApplicantInfo;
    }

    private static ApplicationWizardViewModel BuildViewModel(
        RentalApplication application, WizardStep step, ApplicationWizardViewModel? overlay = null)
    {
        return new ApplicationWizardViewModel
        {
            Id = application.Id,
            Step = step,
            Status = application.Status,
            IsEditable = RentalApplicationRules.IsEditable(application.Status),
            CanSubmit = RentalApplicationRules.CanSubmit(application.IsApplicantInfoComplete, application.IsResidenceHistoryComplete),
            PropertyName = application.Unit.Property.Name,
            UnitNumber = application.Unit.UnitNumber,
            FullName = overlay?.FullName ?? application.FullName,
            Phone = overlay?.Phone ?? application.Phone,
            Email = overlay?.Email ?? application.Email,
            CurrentAddress = overlay?.CurrentAddress ?? application.CurrentAddress,
            ResidenceHistories = application.ResidenceHistories
                .OrderBy(r => r.MoveInDate)
                .Select(r => new ResidenceHistoryItemViewModel
                {
                    Id = r.Id,
                    Address = r.Address,
                    LandlordName = r.LandlordName,
                    LandlordPhone = r.LandlordPhone,
                    MoveInDate = r.MoveInDate,
                    MoveOutDate = r.MoveOutDate
                })
                .ToList()
        };
    }

    // Manual, step-scoped validation — DataAnnotations on the shared wizard view model would
    // fire for fields the current step's form doesn't even render (see the class comment).
    private void ValidateApplicantInfo(ApplicationWizardViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.FullName))
            ModelState.AddModelError(nameof(model.FullName), "Full name is required.");

        if (string.IsNullOrWhiteSpace(model.Phone))
            ModelState.AddModelError(nameof(model.Phone), "Phone is required.");

        if (string.IsNullOrWhiteSpace(model.Email))
            ModelState.AddModelError(nameof(model.Email), "Email is required.");
        else if (!new EmailAddressAttribute().IsValid(model.Email))
            ModelState.AddModelError(nameof(model.Email), "Enter a valid email address.");

        if (string.IsNullOrWhiteSpace(model.CurrentAddress))
            ModelState.AddModelError(nameof(model.CurrentAddress), "Current address is required.");
    }
}
