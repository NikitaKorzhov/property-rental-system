using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.Services.Properties;

namespace PropertyRentalSystem.Tests.Services.Properties;

public class PropertyServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsAndIsReturnedByGetAll()
    {
        await using var db = TestDb.Create();
        var service = new PropertyService(db);

        await service.CreateAsync("Maple Court", "1 Maple St");

        var all = await service.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Maple Court", all[0].Name);
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ReturnsNull()
    {
        await using var db = TestDb.Create();
        var service = new PropertyService(db);

        var result = await service.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_OverwritesFields()
    {
        await using var db = TestDb.Create();
        var service = new PropertyService(db);
        var property = await service.CreateAsync("Old Name", "Old Address");

        await service.UpdateAsync(property, "New Name", "New Address");

        var reloaded = await service.GetByIdAsync(property.Id);
        Assert.Equal("New Name", reloaded!.Name);
        Assert.Equal("New Address", reloaded.Address);
    }

    [Fact]
    public async Task DeleteAsync_WhenNoUnits_Succeeds()
    {
        await using var db = TestDb.Create();
        var service = new PropertyService(db);
        var property = await service.CreateAsync("Empty Lot", "2 Empty St");

        var result = await service.DeleteAsync(property);

        Assert.True(result.Succeeded);
        Assert.Null(await service.GetByIdAsync(property.Id));
    }

    [Fact]
    public async Task DeleteAsync_WhenPropertyHasAtLeastOneUnit_Fails()
    {
        await using var db = TestDb.Create();
        var service = new PropertyService(db);
        var property = await service.CreateAsync("Occupied Lot", "3 Occupied St");
        db.Units.Add(new Unit { PropertyId = property.Id, UnitNumber = "101", UnitTypeId = 1 });
        await db.SaveChangesAsync();

        var result = await service.DeleteAsync(property);

        Assert.False(result.Succeeded);
        Assert.NotNull(await service.GetByIdAsync(property.Id)); // not deleted
    }
}
