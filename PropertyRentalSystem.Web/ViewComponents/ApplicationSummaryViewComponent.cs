using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.ViewModels.Applications;

namespace PropertyRentalSystem.Web.ViewComponents;

// Read-only "both sections" view used by the wizard's Summary step. Designed to be reused
// later by the property manager's review screen, which needs the same read-only summary.
public class ApplicationSummaryViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;

    public ApplicationSummaryViewComponent(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync(int applicationId)
    {
        var application = await _db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.ResidenceHistories)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application == null)
            return Content(string.Empty);

        var model = new ApplicationSummaryViewModel
        {
            PropertyName = application.Unit.Property.Name,
            UnitNumber = application.Unit.UnitNumber,
            FullName = application.FullName,
            Phone = application.Phone,
            Email = application.Email,
            CurrentAddress = application.CurrentAddress,
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

        return View(model);
    }
}
