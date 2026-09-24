using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Domain.Rules;

public static class RentalApplicationRules
{
    // Draft, Submitted, Returned — used both for EF queries (translates to SQL IN) and
    // in-memory checks, so the two never drift apart.
    public static readonly ApplicationStatus[] OpenStatuses =
    {
        ApplicationStatus.Draft, ApplicationStatus.Submitted, ApplicationStatus.Returned
    };

    // An applicant can edit while the application is Draft or Returned; otherwise every
    // section is read-only.
    public static bool IsEditable(ApplicationStatus status) =>
        status is ApplicationStatus.Draft or ApplicationStatus.Returned;

    // Withdraw is allowed from any non-terminal status.
    public static bool IsOpen(ApplicationStatus status) => OpenStatuses.Contains(status);

    // Submit is only available from the Summary once both sections have been saved.
    public static bool CanSubmit(bool isApplicantInfoComplete, bool isResidenceHistoryComplete) =>
        isApplicantInfoComplete && isResidenceHistoryComplete;
}
