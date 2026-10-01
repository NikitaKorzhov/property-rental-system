using PropertyRentalSystem.Web.ViewModels.Applications;

namespace PropertyRentalSystem.Web.Services.Applications;

public interface IApplicationSummaryService
{
    // Read-only "both sections" summary — shared by the applicant's wizard Summary step and
    // the property manager's review details page, so it doesn't belong to either role's service.
    Task<ApplicationSummaryViewModel?> GetSummaryAsync(int applicationId);
}
