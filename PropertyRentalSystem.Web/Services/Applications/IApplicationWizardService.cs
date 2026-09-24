using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Services.Applications;

public interface IApplicationWizardService
{
    // Filtered by status and property, done in the database.
    Task<List<RentalApplication>> GetMyApplicationsAsync(string applicantId, ApplicationStatus? status, int? propertyId);

    Task<RentalApplication?> GetOwnedAsync(int applicationId, string applicantId);

    // Non-editable applications always resume at the read-only Summary. Editable ones resume
    // at the first section that still needs work; once both are complete, start over from the
    // top so Continue/Back walks through the real forms instead of landing on Summary.
    WizardStep DetermineStep(RentalApplication application);

    WizardStep GoBack(WizardStep currentStep);

    // The property manager's comment behind the application's current status (Returned/Denied
    // only) — the latest one, in case it was returned and corrected more than once.
    Task<string?> GetReviewCommentAsync(RentalApplication application);

    Task<ServiceResult> SaveApplicantInfoAsync(
        RentalApplication application, string fullName, string phone, string email, string currentAddress);

    Task<ServiceResult> CompleteResidenceHistoryAsync(RentalApplication application);

    // Rejects when either section is incomplete or the unit has picked up an active lease
    // since the applicant started (the approval check applies again at approval time).
    Task<ServiceResult> SubmitAsync(RentalApplication application, string userId);

    Task<ServiceResult> WithdrawAsync(RentalApplication application, string userId);
}
