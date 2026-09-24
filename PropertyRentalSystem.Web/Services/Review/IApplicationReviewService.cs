using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Services.Review;

public interface IApplicationReviewService
{
    // Filtered by status and property, done in the database. Property managers see all applications.
    Task<List<RentalApplication>> GetFilteredAsync(ApplicationStatus? status, int? propertyId);

    Task<RentalApplication?> GetByIdAsync(int id);

    Task<List<ApplicationStatusHistory>> GetHistoryAsync(int applicationId);

    // Null unless the application exists and is currently reviewable (Submitted).
    Task<RentalApplication?> GetReviewableAsync(int id);

    // Approve creates a 12-month lease and is rejected if the unit already has an active one
    // (prevents a second lease). Return/Deny just record the outcome. The comment being
    // required for Return/Deny is validated by the view model, not re-checked here.
    Task<ServiceResult> ReviewAsync(RentalApplication application, ReviewOutcome outcome, string? comment, string reviewerUserId);
}
