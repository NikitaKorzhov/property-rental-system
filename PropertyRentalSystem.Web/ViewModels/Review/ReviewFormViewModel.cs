using System.ComponentModel.DataAnnotations;

namespace PropertyRentalSystem.Web.ViewModels.Review;

public class ReviewFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Select an outcome.")]
    public ReviewOutcome? Outcome { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Outcome is ReviewOutcome.Return or ReviewOutcome.Deny && string.IsNullOrWhiteSpace(Comment))
            yield return new ValidationResult(
                "A comment is required when returning or denying an application.", new[] { nameof(Comment) });
    }
}
