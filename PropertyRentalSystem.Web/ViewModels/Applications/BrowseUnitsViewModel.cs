using Microsoft.AspNetCore.Mvc.Rendering;

namespace PropertyRentalSystem.Web.ViewModels.Applications;

public class BrowseUnitsViewModel
{
    public List<BrowseUnitViewModel> Units { get; set; } = new();

    public List<SelectListItem> PropertyOptions { get; set; } = new();
    public List<SelectListItem> UnitTypeOptions { get; set; } = new();
    public List<SelectListItem> BedroomOptions { get; set; } = new();

    public int? PropertyId { get; set; }
    public int? UnitTypeId { get; set; }
    public int? Bedrooms { get; set; }
    public decimal? MaxRent { get; set; }
}
