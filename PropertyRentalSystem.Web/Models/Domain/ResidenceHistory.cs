namespace PropertyRentalSystem.Web.Models.Domain;

public class ResidenceHistory
{
    public int Id { get; set; }
    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;
    
    public string Address { get; set; } = string.Empty;
    public string LandlordName { get; set; } = string.Empty;
    public string LandlordPhone { get; set; } = string.Empty;
    public DateTime MoveInDate { get; set; }
    public DateTime MoveOutDate { get; set; }
}