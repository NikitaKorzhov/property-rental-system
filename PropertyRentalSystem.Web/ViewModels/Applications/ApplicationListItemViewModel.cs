using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.ViewModels.Applications;

public class ApplicationListItemViewModel
{
    public int Id { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public bool IsEditable { get; set; }
    public bool CanWithdraw { get; set; }
}
