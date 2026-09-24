using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.ViewModels.Review;

public class StatusHistoryItemViewModel
{
    public ApplicationStatus Status { get; set; }
    public string ChangedByEmail { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? Comment { get; set; }
}
