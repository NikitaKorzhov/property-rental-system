using Microsoft.AspNetCore.Identity;

namespace PropertyRentalSystem.Web.Models;

public class ApplicationUser : IdentityUser
{
    public ICollection<RentalApplication> Applications { get; set; } = new List<RentalApplication>();
}