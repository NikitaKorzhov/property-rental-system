using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Services.Applications;

public interface IResidenceHistoryService
{
    // Null unless the application exists, is owned by this applicant, and is currently editable.
    Task<RentalApplication?> GetOwnedEditableApplicationAsync(int applicationId, string applicantId);

    // Null unless the residence exists and its parent application is owned/editable by this applicant.
    Task<ResidenceHistory?> GetOwnedEditableResidenceAsync(int residenceId, string applicantId);

    Task<ResidenceHistory> AddAsync(
        int applicationId, string address, string landlordName, string landlordPhone, DateTime moveInDate, DateTime moveOutDate);

    Task UpdateAsync(
        ResidenceHistory residence, string address, string landlordName, string landlordPhone, DateTime moveInDate, DateTime moveOutDate);

    Task DeleteAsync(ResidenceHistory residence);
}
