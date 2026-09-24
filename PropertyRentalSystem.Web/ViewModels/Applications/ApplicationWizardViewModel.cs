using System.ComponentModel.DataAnnotations;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.ViewModels.Applications;

// The one view model driving the whole wizard (per spec). Only the fields relevant to the
// current Step are actually posted by that step's form, so validation is done manually in
// the controller per step rather than via DataAnnotations — [Display] is still safe to use
// since it only affects label text, not ModelState.
public class ApplicationWizardViewModel
{
    public int Id { get; set; }
    public WizardStep Step { get; set; }
    public ApplicationStatus Status { get; set; }
    public bool IsEditable { get; set; }
    public bool CanSubmit { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;

    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Display(Name = "Current Address")]
    public string CurrentAddress { get; set; } = string.Empty;

    public List<ResidenceHistoryItemViewModel> ResidenceHistories { get; set; } = new();
}
