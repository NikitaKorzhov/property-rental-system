namespace PropertyRentalSystem.Web.ViewModels.Units;

public class UnitListComponentViewModel
{
    public int PropertyId { get; set; }
    public List<UnitListItemViewModel> Units { get; set; } = new();
}
