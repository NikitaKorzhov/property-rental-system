using Microsoft.AspNetCore.Mvc;
using PropertyRentalSystem.Web.Services.Units;
using PropertyRentalSystem.Web.ViewModels.Units;

namespace PropertyRentalSystem.Web.ViewComponents.Units;

public class UnitListViewComponent : ViewComponent
{
    private readonly IUnitService _units;

    public UnitListViewComponent(IUnitService units)
    {
        _units = units;
    }

    public async Task<IViewComponentResult> InvokeAsync(int propertyId)
    {
        var units = await _units.GetUnitsForPropertyAsync(propertyId);
        return View(new UnitListComponentViewModel { PropertyId = propertyId, Units = units });
    }
}
