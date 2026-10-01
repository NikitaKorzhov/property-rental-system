using System.ComponentModel.DataAnnotations;
using PropertyRentalSystem.Web.Models;

namespace PropertyRentalSystem.Web.ViewModels.Applications;

// The one view model driving the whole wizard (per spec). Only the fields relevant to the
// current Step are actually posted by that step's form — the ApplicantInfo fields' DataAnnotations
// below are only checked on that step's Continue, so they're harmlessly "invalid" on other steps.
public class ApplicationWizardViewModel
{
    public int Id { get; set; }
    public WizardStep Step { get; set; }
    public ApplicationStatus Status { get; set; }
    public bool IsEditable { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanSubmit { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;

    // The property manager's comment from the most recent Returned/Denied review, if any —
    // so the applicant can see why before correcting and resubmitting.
    public string? ReviewComment { get; set; }

    [Display(Name = "Full Name")]
    [Required(ErrorMessage = "Full name is required.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Current Address")]
    [Required(ErrorMessage = "Current address is required.")]
    public string CurrentAddress { get; set; } = string.Empty;

    public List<ResidenceHistoryItemViewModel> ResidenceHistories { get; set; } = new();
}
