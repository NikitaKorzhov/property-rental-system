using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.ViewModels.Review;

public class ApplicationDetailsViewModel
{
    public int Id { get; set; }
    public ApplicationStatus Status { get; set; }
    public bool CanReview { get; set; }
    public List<StatusHistoryItemViewModel> History { get; set; } = new();
}
