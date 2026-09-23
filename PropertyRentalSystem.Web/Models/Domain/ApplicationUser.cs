using Microsoft.AspNetCore.Identity;

namespace PropertyRentalSystem.Web.Models.Domain;

public class ApplicationUser : IdentityUser
{
    public ICollection<RentalApplication> Applications { get; set; } = new List<RentalApplication>();
}