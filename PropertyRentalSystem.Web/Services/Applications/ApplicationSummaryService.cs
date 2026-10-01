using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.ViewModels.Applications;

namespace PropertyRentalSystem.Web.Services.Applications;

public class ApplicationSummaryService : IApplicationSummaryService
{
    private readonly ApplicationDbContext _db;

    public ApplicationSummaryService(ApplicationDbContext db)
    {
        _db = db;
    }

    // Single query, projected straight to the ViewModel (Rule 9) — no separate Include +
    // in-memory mapping, so there's no risk of a second round trip per residence row.
    public async Task<ApplicationSummaryViewModel?> GetSummaryAsync(int applicationId) =>
        await _db.RentalApplications
            .Where(a => a.Id == applicationId)
            .Select(a => new ApplicationSummaryViewModel
            {
                PropertyName = a.Unit.Property.Name,
                UnitNumber = a.Unit.UnitNumber,
                FullName = a.FullName,
                Phone = a.Phone,
                Email = a.Email,
                CurrentAddress = a.CurrentAddress,
                ResidenceHistories = a.ResidenceHistories
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
            })
            .FirstOrDefaultAsync();
}
