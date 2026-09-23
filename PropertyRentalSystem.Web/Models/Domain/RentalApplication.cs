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

    // Required when Status is Return or Deny
    public string? ReviewComment { get; set; }
    
    public ICollection<ResidenceHistory> ResidenceHistories { get; set; } = new List<ResidenceHistory>();
}