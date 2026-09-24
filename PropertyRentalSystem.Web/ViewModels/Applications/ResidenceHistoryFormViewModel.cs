using System.ComponentModel.DataAnnotations;

namespace PropertyRentalSystem.Web.ViewModels.Applications;

public class ResidenceHistoryFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    [Required]
    public int RentalApplicationId { get; set; }

    [Required, StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Landlord Name")]
    public string LandlordName { get; set; } = string.Empty;

    [Required, StringLength(30)]
    [Display(Name = "Landlord Phone")]
    public string LandlordPhone { get; set; } = string.Empty;

    [Required, DataType(DataType.Date)]
    [Display(Name = "Move-in Date")]
    public DateTime MoveInDate { get; set; } = DateTime.Today.AddYears(-1);

    [Required, DataType(DataType.Date)]
    [Display(Name = "Move-out Date")]
    public DateTime MoveOutDate { get; set; } = DateTime.Today;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MoveOutDate < MoveInDate)
            yield return new ValidationResult("Move-out date can't be before the move-in date.", new[] { nameof(MoveOutDate) });
    }
}
