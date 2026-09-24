using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.Services.Applications;

namespace PropertyRentalSystem.Tests.Services.Applications;

public class ApplicationBrowseServiceTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    private static async Task<(Property property, UnitType type, Unit leasedUnit, Unit freeUnit)> SeedAsync(ApplicationDbContext db)
    {
        var property = new Property { Name = "P1", Address = "1 St" };
        var type = new UnitType { Name = "Studio", IsActive = true };
        db.Properties.Add(property);
        db.UnitTypes.Add(type);
        await db.SaveChangesAsync();

        var leasedUnit = new Unit { PropertyId = property.Id, UnitTypeId = type.Id, UnitNumber = "101", Bedrooms = 0, MonthlyRent = 900 };
        var freeUnit = new Unit { PropertyId = property.Id, UnitTypeId = type.Id, UnitNumber = "102", Bedrooms = 0, MonthlyRent = 900 };
        db.Units.AddRange(leasedUnit, freeUnit);
        await db.SaveChangesAsync();

        db.Leases.Add(new Lease { UnitId = leasedUnit.Id, StartDate = Today.AddDays(-10), EndDate = Today.AddDays(10) });
        await db.SaveChangesAsync();

        return (property, type, leasedUnit, freeUnit);
    }

    [Fact]
    public async Task GetAvailableUnitsAsync_ExcludesUnitWithActiveLease()
    {
        await using var db = TestDb.Create();
        var (_, _, leasedUnit, freeUnit) = await SeedAsync(db);
        var service = new ApplicationBrowseService(db);

        var available = await service.GetAvailableUnitsAsync(null, null, null, null);

        Assert.DoesNotContain(available, u => u.Id == leasedUnit.Id);
        Assert.Contains(available, u => u.Id == freeUnit.Id);
    }

    [Fact]
    public async Task GetAvailableUnitsAsync_UnitBecomesAvailableTheDayAfterLeaseEnds()
    {
        await using var db = TestDb.Create();
        var property = new Property { Name = "P1", Address = "1 St" };
        var type = new UnitType { Name = "Studio", IsActive = true };
        db.Properties.Add(property);
        db.UnitTypes.Add(type);
        await db.SaveChangesAsync();
        var unit = new Unit { PropertyId = property.Id, UnitTypeId = type.Id, UnitNumber = "101" };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        // Lease ended yesterday.
        db.Leases.Add(new Lease { UnitId = unit.Id, StartDate = Today.AddMonths(-12), EndDate = Today.AddDays(-1) });
        await db.SaveChangesAsync();
        var service = new ApplicationBrowseService(db);

        var available = await service.GetAvailableUnitsAsync(null, null, null, null);

        Assert.Contains(available, u => u.Id == unit.Id);
    }

    [Fact]
    public async Task GetAvailableUnitsAsync_FiltersByMaxRent()
    {
        await using var db = TestDb.Create();
        var (_, _, _, freeUnit) = await SeedAsync(db);
        var service = new ApplicationBrowseService(db);

        var tooLow = await service.GetAvailableUnitsAsync(null, null, null, maxRent: 500m);
        var enough = await service.GetAvailableUnitsAsync(null, null, null, maxRent: 900m);

        Assert.DoesNotContain(tooLow, u => u.Id == freeUnit.Id);
        Assert.Contains(enough, u => u.Id == freeUnit.Id);
    }

    [Fact]
    public async Task StartApplicationAsync_WhenUnitHasActiveLease_Fails()
    {
        await using var db = TestDb.Create();
        var (_, _, leasedUnit, _) = await SeedAsync(db);
        var service = new ApplicationBrowseService(db);

        var result = await service.StartApplicationAsync(leasedUnit, "applicant-1", "a@test.com");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task StartApplicationAsync_WhenNoExistingApplication_CreatesADraft()
    {
        await using var db = TestDb.Create();
        var (_, _, _, freeUnit) = await SeedAsync(db);
        var service = new ApplicationBrowseService(db);

        var result = await service.StartApplicationAsync(freeUnit, "applicant-1", "a@test.com");

        Assert.True(result.Succeeded);
        Assert.Equal(ApplicationStatus.Draft, result.Value!.Status);
        Assert.Equal("a@test.com", result.Value.Email);
    }

    [Fact]
    public async Task StartApplicationAsync_WhenAnOpenApplicationAlreadyExists_ReusesItInsteadOfCreatingANewOne()
    {
        await using var db = TestDb.Create();
        var (_, _, _, freeUnit) = await SeedAsync(db);
        var service = new ApplicationBrowseService(db);
        var first = await service.StartApplicationAsync(freeUnit, "applicant-1", "a@test.com");

        var second = await service.StartApplicationAsync(freeUnit, "applicant-1", "a@test.com");

        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Single(db.RentalApplications);
    }
}
