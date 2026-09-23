namespace PropertyRentalSystem.Web.Models.Domain;

public class ApplicationStatusHistory
{
    public int Id { get; set; }

    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public ApplicationStatus Status { get; set; }

    public string ChangedByUserId { get; set; } = string.Empty;
    public ApplicationUser ChangedBy { get; set; } = null!;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    // Required when Status is Returned or Denied
    public string? Comment { get; set; }
}
