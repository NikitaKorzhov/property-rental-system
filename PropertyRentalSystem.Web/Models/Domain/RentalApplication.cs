namespace PropertyRentalSystem.Web.Models.Domain;

public class RentalApplication
{
    public int Id { get; set; }

    public string ApplicantId { get; set; } = string.Empty;
    public ApplicationUser Applicant { get; set; } = null!;

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CurrentAddress { get; set; } = string.Empty;

    // Tracks whether each wizard section was explicitly saved via "Continue" — an empty
    // ResidenceHistories collection alone can't tell "not visited yet" from "no prior residences".
    // Submit is only allowed from the Summary once both are true.
    public bool IsApplicantInfoComplete { get; set; }
    public bool IsResidenceHistoryComplete { get; set; }

    public ICollection<ResidenceHistory> ResidenceHistories { get; set; } = new List<ResidenceHistory>();
    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();
}