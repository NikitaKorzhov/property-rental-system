using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.Services.Units;

namespace PropertyRentalSystem.Tests.Services.Units;

public class UnitServiceTests
{
    private static async Task<(Property property, UnitType active, UnitType inactive)> SeedAsync(
        Web.Data.ApplicationDbContext db)
    {
        var property = new Property { Name = "P1", Address = "1 St" };
        var active = new UnitType { Name = "Studio", IsActive = true };
        var inactive = new UnitType { Name = "2-Bedroom", IsActive = false };
        db.Properties.Add(property);
        db.UnitTypes.AddRange(active, inactive);
        await db.SaveChangesAsync();
        return (property, active, inactive);
    }

    [Fact]
    public async Task CreateAsync_WithActiveType_Succeeds()
    {
        await using var db = TestDb.Create();
        var (property, active, _) = await SeedAsync(db);
        var service = new UnitService(db);

        var result = await service.CreateAsync(property.Id, "101", 1, 900m, active.Id);

        Assert.True(result.Succeeded);
        Assert.Equal("101", result.Value!.UnitNumber);
    }

    [Fact]
    public async Task CreateAsync_WithInactiveType_FailsOnUnitTypeIdField()
    {
        await using var db = TestDb.Create();
        var (property, _, inactive) = await SeedAsync(db);
        var service = new UnitService(db);

        var result = await service.CreateAsync(property.Id, "101", 2, 900m, inactive.Id);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Field == "UnitTypeId");
    }

    [Fact]
    public async Task CreateAsync_WithUnknownProperty_Fails()
    {
        await using var db = TestDb.Create();
        var (_, active, _) = await SeedAsync(db);
        var service = new UnitService(db);

        var result = await service.CreateAsync(propertyId: 999, "101", 1, 900m, active.Id);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateAsync_DuplicateNumberInSameProperty_FailsOnUnitNumberField()
    {
        await using var db = TestDb.Create();
        var (property, active, _) = await SeedAsync(db);
        var service = new UnitService(db);
        await service.CreateAsync(property.Id, "101", 1, 900m, active.Id);

        var result = await service.CreateAsync(property.Id, "101", 2, 950m, active.Id);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Field == "UnitNumber");
    }

    [Fact]
    public async Task CreateAsync_SameNumberInDifferentProperty_Succeeds()
    {
        await using var db = TestDb.Create();
        var (property1, active, _) = await SeedAsync(db);
        var property2 = new Property { Name = "P2", Address = "2 St" };
        db.Properties.Add(property2);
        await db.SaveChangesAsync();
        var service = new UnitService(db);
        await service.CreateAsync(property1.Id, "101", 1, 900m, active.Id);

        var result = await service.CreateAsync(property2.Id, "101", 1, 900m, active.Id);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UpdateAsync_LeavingOwnInactiveTypeUnchanged_Succeeds()
    {
        await using var db = TestDb.Create();
        var (property, _, inactive) = await SeedAsync(db);
        var unit = new Unit { PropertyId = property.Id, UnitNumber = "101", UnitTypeId = inactive.Id, Bedrooms = 2 };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        var service = new UnitService(db);

        // Same unit type the unit already has — allowed even though it's inactive.
        var result = await service.UpdateAsync(unit, "101", 2, 950m, inactive.Id);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UpdateAsync_SwitchingToADifferentInactiveType_Fails()
    {
        await using var db = TestDb.Create();
        var (property, active, inactive) = await SeedAsync(db);
        var unit = new Unit { PropertyId = property.Id, UnitNumber = "101", UnitTypeId = active.Id, Bedrooms = 1 };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        var service = new UnitService(db);

        var result = await service.UpdateAsync(unit, "101", 1, 900m, inactive.Id);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Field == "UnitTypeId");
    }

    [Fact]
    public async Task UpdateAsync_KeepingItsOwnNumber_DoesNotFlagItselfAsDuplicate()
    {
        await using var db = TestDb.Create();
        var (property, active, _) = await SeedAsync(db);
        var unit = new Unit { PropertyId = property.Id, UnitNumber = "101", UnitTypeId = active.Id, Bedrooms = 1 };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        var service = new UnitService(db);

        var result = await service.UpdateAsync(unit, "101", 1, 950m, active.Id);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task DeleteAsync_WhenUnitHasAnApplication_Fails()
    {
        await using var db = TestDb.Create();
        var (property, active, _) = await SeedAsync(db);
        var unit = new Unit { PropertyId = property.Id, UnitNumber = "101", UnitTypeId = active.Id };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        db.RentalApplications.Add(new RentalApplication { UnitId = unit.Id, ApplicantId = "user-1" });
        await db.SaveChangesAsync();
        var service = new UnitService(db);

        var result = await service.DeleteAsync(unit);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task DeleteAsync_WhenUnitHasNoApplicationsOrLease_Succeeds()
    {
        await using var db = TestDb.Create();
        var (property, active, _) = await SeedAsync(db);
        var unit = new Unit { PropertyId = property.Id, UnitNumber = "101", UnitTypeId = active.Id };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        var service = new UnitService(db);

        var result = await service.DeleteAsync(unit);

        Assert.True(result.Succeeded);
    }
}
