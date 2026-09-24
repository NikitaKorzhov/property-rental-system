using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyRentalSystem.Web.Domain.Rules;
using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.Services.Applications;
using PropertyRentalSystem.Web.Services.Properties;
using PropertyRentalSystem.Web.ViewModels.Applications;
using PropertyRentalSystem.Web.ViewModels.Shared;

namespace PropertyRentalSystem.Web.Controllers;

[Authorize(Roles = Roles.Applicant)]
public class ApplicationsController : ModalFormControllerBase
{
    private readonly IApplicationBrowseService _browse;
    private readonly IApplicationWizardService _wizard;
    private readonly IResidenceHistoryService _residences;
    private readonly IPropertyService _properties;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationsController(
        IApplicationBrowseService browse,
        IApplicationWizardService wizard,
        IResidenceHistoryService residences,
        IPropertyService properties,
        UserManager<ApplicationUser> userManager)
    {
        _browse = browse;
        _wizard = wizard;
        _residences = residences;
        _properties = properties;
        _userManager = userManager;
    }

    private string CurrentUserId => _userManager.GetUserId(User)!;

    // ---------- My applications ----------

    public async Task<IActionResult> Index(ApplicationStatus? status, int? propertyId)
    {
        var applications = await _wizard.GetMyApplicationsAsync(CurrentUserId, status, propertyId);
        var properties = await _properties.GetAllAsync();

        return View(new ApplicationListViewModel
        {
            Applications = applications
                .Select(a => new ApplicationListItemViewModel
                {
                    Id = a.Id,
                    PropertyName = a.Unit.Property.Name,
                    UnitNumber = a.Unit.UnitNumber,
                    Status = a.Status
                })
                .ToList(),
            SelectedStatus = status,
            SelectedPropertyId = propertyId,
            StatusOptions = Enum.GetValues<ApplicationStatus>()
                .Select(s => new SelectListItem(s.ToString(), s.ToString(), s == status))
                .ToList(),
            PropertyOptions = properties
                .Select(p => new SelectListItem(p.Name, p.Id.ToString(), p.Id == propertyId))
                .ToList()
        });
    }

    // ---------- Browse available units ----------

    public async Task<IActionResult> Browse(int? propertyId, int? unitTypeId, int? bedrooms, decimal? maxRent)
    {
        var units = await _browse.GetAvailableUnitsAsync(propertyId, unitTypeId, bedrooms, maxRent);
        var openApplicationsByUnit = await _browse.GetOpenApplicationUnitMapAsync(CurrentUserId);

        var unitViewModels = units.Select(u => new BrowseUnitViewModel
        {
            Id = u.Id,
            PropertyName = u.Property.Name,
            PropertyAddress = u.Property.Address,
            UnitNumber = u.UnitNumber,
            Bedrooms = u.Bedrooms,
            MonthlyRent = u.MonthlyRent,
            UnitTypeName = u.UnitType.Name,
            ExistingApplicationId = openApplicationsByUnit.TryGetValue(u.Id, out var appId) ? appId : null
        }).ToList();

        var properties = await _properties.GetAllAsync();
        var unitTypes = await _browse.GetUnitTypesAsync();
        var bedroomCounts = await _browse.GetDistinctBedroomCountsAsync();

        return View(new BrowseUnitsViewModel
        {
            Units = unitViewModels,
            PropertyOptions = properties
                .Select(p => new SelectListItem(p.Name, p.Id.ToString(), p.Id == propertyId))
                .ToList(),
            UnitTypeOptions = unitTypes
                .Select(t => new SelectListItem(t.Name, t.Id.ToString(), t.Id == unitTypeId))
                .ToList(),
            BedroomOptions = bedroomCounts
                .Select(b => new SelectListItem(b.ToString(), b.ToString(), b == bedrooms))
                .ToList(),
            PropertyId = propertyId,
            UnitTypeId = unitTypeId,
            Bedrooms = bedrooms,
            MaxRent = maxRent
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int unitId)
    {
        var unit = await _browse.GetUnitAsync(unitId);
        if (unit == null) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        var result = await _browse.StartApplicationAsync(unit, CurrentUserId, currentUser?.Email);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Errors[0].Message;
            return RedirectToAction(nameof(Browse));
        }

        return RedirectToAction(nameof(Wizard), new { id = result.Value!.Id });
    }

    // ---------- The wizard ----------

    [HttpGet]
    public async Task<IActionResult> Wizard(int id)
    {
        var application = await _wizard.GetOwnedAsync(id, CurrentUserId);
        if (application == null) return NotFound();

        var step = _wizard.DetermineStep(application);
        var reviewComment = await _wizard.GetReviewCommentAsync(application);
        return View(BuildViewModel(application, step, reviewComment: reviewComment));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Wizard(ApplicationWizardViewModel model, string action)
    {
        var application = await _wizard.GetOwnedAsync(model.Id, CurrentUserId);
        if (application == null) return NotFound();

        var reviewComment = await _wizard.GetReviewCommentAsync(application);

        // Controllers reject posts that are not allowed: only Draft/Returned may be edited or
        // submitted. Stale client state (browser back/forward cache, a second tab, a resubmitted
        // old page) shouldn't dead-end on a blank error — send the user back to the wizard,
        // which recomputes the real current step from the database.
        if (!RentalApplicationRules.IsEditable(application.Status))
            return RedirectToAction(nameof(Wizard), new { id = application.Id });

        switch (action)
        {
            case "Back":
                // We're re-rendering a different step than what was posted — clear ModelState
                // so asp-for reads the fresh model (e.g. the new Step) instead of redisplaying
                // whatever was in the just-submitted form for those same field names.
                ModelState.Clear();
                return View(BuildViewModel(application, _wizard.GoBack(model.Step), reviewComment: reviewComment));

            case "Continue" when model.Step == WizardStep.ApplicantInfo:
                var infoResult = await _wizard.SaveApplicantInfoAsync(
                    application, model.FullName, model.Phone, model.Email, model.CurrentAddress);
                if (!infoResult.Succeeded)
                {
                    foreach (var error in infoResult.Errors)
                        ModelState.AddModelError(error.Field, error.Message);
                    return View(BuildViewModel(application, WizardStep.ApplicantInfo, model, reviewComment));
                }

                ModelState.Clear();
                return View(BuildViewModel(application, WizardStep.ResidenceHistory, reviewComment: reviewComment));

            case "Continue" when model.Step == WizardStep.ResidenceHistory:
                await _wizard.CompleteResidenceHistoryAsync(application);

                ModelState.Clear();
                return View(BuildViewModel(application, WizardStep.Summary, reviewComment: reviewComment));

            case "Submit" when model.Step == WizardStep.Summary:
                var submitResult = await _wizard.SubmitAsync(application, CurrentUserId);
                if (!submitResult.Succeeded)
                {
                    foreach (var error in submitResult.Errors)
                        ModelState.AddModelError(error.Field, error.Message);
                    return View(BuildViewModel(application, WizardStep.Summary, reviewComment: reviewComment));
                }

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
        var application = await _residences.GetOwnedEditableApplicationAsync(applicationId, CurrentUserId);
        if (application == null) return NotFound();

        return PartialView("_ResidenceForm", new ResidenceHistoryFormViewModel { RentalApplicationId = applicationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddResidence(ResidenceHistoryFormViewModel model)
    {
        var application = await _residences.GetOwnedEditableApplicationAsync(model.RentalApplicationId, CurrentUserId);
        if (application == null) return NotFound();

        if (!ModelState.IsValid)
            return PartialView("_ResidenceForm", model);

        await _residences.AddAsync(
            model.RentalApplicationId, model.Address, model.LandlordName, model.LandlordPhone, model.MoveInDate, model.MoveOutDate);

        return FormSuccess();
    }

    [HttpGet]
    public async Task<IActionResult> EditResidence(int id)
    {
        var residence = await _residences.GetOwnedEditableResidenceAsync(id, CurrentUserId);
        if (residence == null) return NotFound();

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

        var residence = await _residences.GetOwnedEditableResidenceAsync(id, CurrentUserId);
        if (residence == null) return NotFound();

        if (!ModelState.IsValid)
            return PartialView("_ResidenceForm", model);

        await _residences.UpdateAsync(
            residence, model.Address, model.LandlordName, model.LandlordPhone, model.MoveInDate, model.MoveOutDate);

        return FormSuccess();
    }

    [HttpGet]
    public async Task<IActionResult> DeleteResidenceConfirm(int id)
    {
        var residence = await _residences.GetOwnedEditableResidenceAsync(id, CurrentUserId);
        if (residence == null) return NotFound();

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
        var residence = await _residences.GetOwnedEditableResidenceAsync(id, CurrentUserId);
        if (residence == null) return NotFound();

        await _residences.DeleteAsync(residence);
        return FormSuccess();
    }

    // Re-rendered into #residence-history-section by modal-forms.js after a residence add/edit/delete.
    [HttpGet]
    public async Task<IActionResult> ResidenceHistorySection(int applicationId)
    {
        var application = await _wizard.GetOwnedAsync(applicationId, CurrentUserId);
        if (application == null) return NotFound();

        return PartialView("_ResidenceHistorySection", BuildViewModel(application, WizardStep.ResidenceHistory));
    }

    // ---------- Withdraw ----------

    [HttpGet]
    public async Task<IActionResult> WithdrawConfirm(int id)
    {
        var application = await _wizard.GetOwnedAsync(id, CurrentUserId);
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
        var application = await _wizard.GetOwnedAsync(id, CurrentUserId);
        if (application == null) return NotFound();

        var result = await _wizard.WithdrawAsync(application, CurrentUserId);
        if (!result.Succeeded)
            return NotFound();

        return FormSuccess();
    }

    // ---------- Helpers ----------

    private static ApplicationWizardViewModel BuildViewModel(
        RentalApplication application, WizardStep step, ApplicationWizardViewModel? overlay = null, string? reviewComment = null)
    {
        return new ApplicationWizardViewModel
        {
            Id = application.Id,
            Step = step,
            Status = application.Status,
            IsEditable = RentalApplicationRules.IsEditable(application.Status),
            CanSubmit = application.IsApplicantInfoComplete && application.IsResidenceHistoryComplete,
            PropertyName = application.Unit.Property.Name,
            UnitNumber = application.Unit.UnitNumber,
            ReviewComment = reviewComment,
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
}
