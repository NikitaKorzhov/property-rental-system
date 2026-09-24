using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.ViewModels.Units;

namespace PropertyRentalSystem.Web.ViewComponents;

public class UnitListViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;

    public UnitListViewComponent(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync(int propertyId)
    {
        var units = await _db.Units
            .Where(u => u.PropertyId == propertyId)
            .Include(u => u.UnitType)
            .OrderBy(u => u.UnitNumber)
            .Select(u => new UnitListItemViewModel
            {
                Id = u.Id,
                UnitNumber = u.UnitNumber,
                Bedrooms = u.Bedrooms,
                MonthlyRent = u.MonthlyRent,
                UnitTypeName = u.UnitType.Name,
                UnitTypeIsActive = u.UnitType.IsActive
            })
            .ToListAsync();

        return View(new UnitListComponentViewModel { PropertyId = propertyId, Units = units });
    }
}
