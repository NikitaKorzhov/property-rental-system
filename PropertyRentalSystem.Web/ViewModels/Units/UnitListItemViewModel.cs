namespace PropertyRentalSystem.Web.ViewModels.Units;

public class UnitListItemViewModel
{
    public int Id { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
    public int Bedrooms { get; set; }
    public decimal MonthlyRent { get; set; }
    public string UnitTypeName { get; set; } = string.Empty;
    public bool UnitTypeIsActive { get; set; }
}
