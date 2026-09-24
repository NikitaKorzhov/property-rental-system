using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.Services.Properties;
using PropertyRentalSystem.Web.Services.Review;
using PropertyRentalSystem.Web.ViewModels.Review;

namespace PropertyRentalSystem.Web.Controllers;

[Authorize(Roles = Roles.PropertyManager)]
public class ApplicationReviewController : ModalFormControllerBase
{
    private readonly IApplicationReviewService _review;
    private readonly IPropertyService _properties;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationReviewController(
        IApplicationReviewService review, IPropertyService properties, UserManager<ApplicationUser> userManager)
    {
        _review = review;
        _properties = properties;
        _userManager = userManager;
    }

    private string CurrentUserId => _userManager.GetUserId(User)!;

    public async Task<IActionResult> Index(ApplicationStatus? status, int? propertyId)
    {
        var applications = await _review.GetFilteredAsync(status, propertyId);
        var properties = await _properties.GetAllAsync();

        return View(new PmApplicationListViewModel
        {
            Applications = applications
                .Select(a => new PmApplicationListItemViewModel
                {
                    Id = a.Id,
                    ApplicantEmail = a.Applicant.Email!,
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

    public async Task<IActionResult> Details(int id)
    {
        var application = await _review.GetByIdAsync(id);
        if (application == null) return NotFound();

        var history = await _review.GetHistoryAsync(id);

        return View(new ApplicationDetailsViewModel
        {
            Id = application.Id,
            Status = application.Status,
            CanReview = application.Status == ApplicationStatus.Submitted,
            History = history
                .Select(h => new StatusHistoryItemViewModel
                {
                    Status = h.Status,
                    ChangedByEmail = h.ChangedBy.Email!,
                    ChangedAt = h.ChangedAt,
                    Comment = h.Comment
                })
                .ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> ReviewConfirm(int id)
    {
        var application = await _review.GetReviewableAsync(id);
        if (application == null) return NotFound();

        return PartialView("_ReviewForm", new ReviewFormViewModel { Id = id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(ReviewFormViewModel model)
    {
        var application = await _review.GetReviewableAsync(model.Id);
        if (application == null) return NotFound();

        if (!ModelState.IsValid)
            return PartialView("_ReviewForm", model);

        var result = await _review.ReviewAsync(application, model.Outcome!.Value, model.Comment, CurrentUserId);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Field, error.Message);
            return PartialView("_ReviewForm", model);
        }

        return FormSuccess();
    }
}
