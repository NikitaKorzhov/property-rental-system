namespace PropertyRentalSystem.Web.ViewModels.Applications;

public class ResidenceHistoryItemViewModel
{
    public int Id { get; set; }
    public string Address { get; set; } = string.Empty;
    public string LandlordName { get; set; } = string.Empty;
    public string LandlordPhone { get; set; } = string.Empty;
    public DateTime MoveInDate { get; set; }
    public DateTime MoveOutDate { get; set; }
}
