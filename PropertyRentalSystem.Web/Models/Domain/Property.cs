namespace PropertyRentalSystem.Web.Models.Domain;

public class Property
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}