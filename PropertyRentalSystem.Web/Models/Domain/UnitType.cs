namespace PropertyRentalSystem.Web.Models.Domain;

public class UnitType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}