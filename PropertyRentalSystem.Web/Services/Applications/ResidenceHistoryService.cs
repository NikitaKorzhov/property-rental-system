using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Domain.Rules;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Services.Applications;

public class ResidenceHistoryService : IResidenceHistoryService
{
    private readonly ApplicationDbContext _db;

    public ResidenceHistoryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<RentalApplication?> GetOwnedEditableApplicationAsync(int applicationId, string applicantId)
    {
        var application = await _db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.ResidenceHistories)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        return application != null && IsOwnedAndEditable(application, applicantId) ? application : null;
    }

    public async Task<ResidenceHistory?> GetOwnedEditableResidenceAsync(int residenceId, string applicantId)
    {
        var residence = await _db.ResidenceHistories
            .Include(r => r.RentalApplication)
            .FirstOrDefaultAsync(r => r.Id == residenceId);

        return residence != null && IsOwnedAndEditable(residence.RentalApplication, applicantId) ? residence : null;
    }

    public async Task<ResidenceHistory> AddAsync(
        int applicationId, string address, string landlordName, string landlordPhone, DateTime moveInDate, DateTime moveOutDate)
    {
        var residence = new ResidenceHistory
        {
            RentalApplicationId = applicationId,
            Address = address,
            LandlordName = landlordName,
            LandlordPhone = landlordPhone,
            MoveInDate = moveInDate,
            MoveOutDate = moveOutDate
        };
        _db.ResidenceHistories.Add(residence);
        await _db.SaveChangesAsync();
        return residence;
    }

    public async Task UpdateAsync(
        ResidenceHistory residence, string address, string landlordName, string landlordPhone, DateTime moveInDate, DateTime moveOutDate)
    {
        residence.Address = address;
        residence.LandlordName = landlordName;
        residence.LandlordPhone = landlordPhone;
        residence.MoveInDate = moveInDate;
        residence.MoveOutDate = moveOutDate;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(ResidenceHistory residence)
    {
        _db.ResidenceHistories.Remove(residence);
        await _db.SaveChangesAsync();
    }

    private static bool IsOwnedAndEditable(RentalApplication application, string applicantId) =>
        application.ApplicantId == applicantId && RentalApplicationRules.IsEditable(application.Status);
}
