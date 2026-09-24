namespace PropertyRentalSystem.Web.ViewModels.Applications;

public class ApplicationSummaryViewModel
{
    public string PropertyName { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CurrentAddress { get; set; } = string.Empty;
    public List<ResidenceHistoryItemViewModel> ResidenceHistories { get; set; } = new();
}
