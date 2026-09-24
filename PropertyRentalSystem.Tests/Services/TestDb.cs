using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;

namespace PropertyRentalSystem.Tests.Services;

// Each call gets its own isolated in-memory database (unique name), so tests never leak
// state into one another even when run in parallel.
public static class TestDb
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
