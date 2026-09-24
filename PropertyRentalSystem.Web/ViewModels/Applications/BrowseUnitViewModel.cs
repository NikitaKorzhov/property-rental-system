namespace PropertyRentalSystem.Web.ViewModels.Applications;

public class BrowseUnitViewModel
{
    public int Id { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public int Bedrooms { get; set; }
    public decimal MonthlyRent { get; set; }
    public string UnitTypeName { get; set; } = string.Empty;

    // Set when the current applicant already has an open (Draft/Submitted/Returned)
    // application for this unit, so Browse can offer "Continue" instead of "Apply".
    public int? ExistingApplicationId { get; set; }
}
