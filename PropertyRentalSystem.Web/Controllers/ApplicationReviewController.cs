using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Domain.Rules;
using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.ViewModels.Review;

namespace PropertyRentalSystem.Web.Controllers;

[Authorize(Roles = Roles.PropertyManager)]
public class ApplicationReviewController : ModalFormControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationReviewController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private string CurrentUserId => _userManager.GetUserId(User)!;

    // Filtering is done in the database (translated to SQL WHERE), not in memory.
    public async Task<IActionResult> Index(ApplicationStatus? status, int? propertyId)
    {
        var query = _db.RentalApplications.AsQueryable();
        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);
        if (propertyId.HasValue)
            query = query.Where(a => a.Unit.PropertyId == propertyId.Value);

        var applications = await query
            .OrderByDescending(a => a.Id)
            .Select(a => new PmApplicationListItemViewModel
            {
                Id = a.Id,
                ApplicantEmail = a.Applicant.Email!,
                PropertyName = a.Unit.Property.Name,
                UnitNumber = a.Unit.UnitNumber,
                Status = a.Status
            })
            .ToListAsync();

        var properties = await _db.Properties.OrderBy(p => p.Name).ToListAsync();

        return View(new PmApplicationListViewModel
        {
            Applications = applications,
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

    public async Task<IActionResult> Details(int id)
    {
        var application = await _db.RentalApplications.FindAsync(id);
        if (application == null) return NotFound();

        var history = await _db.ApplicationStatusHistories
            .Where(h => h.RentalApplicationId == id)
            .OrderBy(h => h.ChangedAt)
            .Select(h => new StatusHistoryItemViewModel
            {
                Status = h.Status,
                ChangedByEmail = h.ChangedBy.Email!,
                ChangedAt = h.ChangedAt,
                Comment = h.Comment
            })
            .ToListAsync();

        return View(new ApplicationDetailsViewModel
        {
            Id = application.Id,
            Status = application.Status,
            CanReview = application.Status == ApplicationStatus.Submitted,
            History = history
        });
    }

    [HttpGet]
    public async Task<IActionResult> ReviewConfirm(int id)
    {
        var application = await _db.RentalApplications.FindAsync(id);
        if (application == null || application.Status != ApplicationStatus.Submitted) return NotFound();

        return PartialView("_ReviewForm", new ReviewFormViewModel { Id = id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(ReviewFormViewModel model)
    {
        var application = await _db.RentalApplications.FindAsync(model.Id);
        // Controllers reject posts that are not allowed: only a Submitted application can be reviewed.
        if (application == null || application.Status != ApplicationStatus.Submitted)
            return NotFound();

        if (!ModelState.IsValid)
            return PartialView("_ReviewForm", model);

        if (model.Outcome == ReviewOutcome.Approve)
        {
            var today = DateTime.UtcNow.Date;
            var unitLeases = await _db.Leases.Where(l => l.UnitId == application.UnitId).ToListAsync();
            var hasActiveLease = unitLeases.Any(l => LeaseRules.CoversDate(l.StartDate, l.EndDate, today));
            if (hasActiveLease)
            {
                // The approval check prevents a second lease. Other open applications for
                // this unit are left as they are — only this one is rejected.
                ModelState.AddModelError(string.Empty,
                    "This unit already has an active lease. Approval is blocked to prevent a second lease.");
                return PartialView("_ReviewForm", model);
            }

            application.Status = ApplicationStatus.Approved;
            application.Lease = new Lease
            {
                UnitId = application.UnitId,
                StartDate = today,
                EndDate = today.AddMonths(12)
            };
        }
        else if (model.Outcome == ReviewOutcome.Return)
        {
            application.Status = ApplicationStatus.Returned;
        }
        else
        {
            application.Status = ApplicationStatus.Denied;
        }

        _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            RentalApplicationId = application.Id,
            Status = application.Status,
            ChangedByUserId = CurrentUserId,
            ChangedAt = DateTime.UtcNow,
            Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim()
        });

        await _db.SaveChangesAsync();
        return FormSuccess();
    }
}
