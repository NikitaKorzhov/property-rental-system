using Microsoft.EntityFrameworkCore;

namespace PropertyRentalSystem.Web.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}
