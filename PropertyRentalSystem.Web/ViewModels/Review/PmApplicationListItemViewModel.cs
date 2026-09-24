using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.ViewModels.Review;

public class PmApplicationListItemViewModel
{
    public int Id { get; set; }
    public string ApplicantEmail { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
}
