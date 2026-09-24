using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.ViewModels.Applications;

public class ApplicationListViewModel
{
    public List<ApplicationListItemViewModel> Applications { get; set; } = new();
    public List<SelectListItem> StatusOptions { get; set; } = new();
    public List<SelectListItem> PropertyOptions { get; set; } = new();
    public ApplicationStatus? SelectedStatus { get; set; }
    public int? SelectedPropertyId { get; set; }
}
