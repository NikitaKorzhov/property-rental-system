using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Models;
using PropertyRentalSystem.Web.Services.Applications;

namespace PropertyRentalSystem.Tests.Services.Applications;

public class ApplicationSummaryServiceTests
{
    private static async Task<RentalApplication> SeedAsync(ApplicationDbContext db)
    {
        var property = new Property { Name = "Grand Towers", Address = "1 St" };
        var type = new UnitType { Name = "Studio", IsActive = true };
        db.Properties.Add(property);
        db.UnitTypes.Add(type);
        await db.SaveChangesAsync();
        var unit = new Unit { PropertyId = property.Id, UnitTypeId = type.Id, UnitNumber = "101" };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        var application = new RentalApplication
        {
            UnitId = unit.Id,
            ApplicantId = "applicant-1",
            FullName = "A B",
            Phone = "123",
            Email = "a@b.com",
            CurrentAddress = "1 Main St"
        };
        application.ResidenceHistories.Add(new ResidenceHistory
        {
            Address = "Newer place",
            LandlordName = "L2",
            LandlordPhone = "222",
            MoveInDate = new DateTime(2023, 1, 1),
            MoveOutDate = new DateTime(2024, 1, 1)
        });
        application.ResidenceHistories.Add(new ResidenceHistory
        {
            Address = "Older place",
            LandlordName = "L1",
            LandlordPhone = "111",
            MoveInDate = new DateTime(2020, 1, 1),
            MoveOutDate = new DateTime(2022, 1, 1)
        });
        db.RentalApplications.Add(application);
        await db.SaveChangesAsync();
        return application;
    }

    [Fact]
    public async Task GetSummaryAsync_ForExistingApplication_MapsApplicantAndUnitFields()
    {
        await using var db = TestDb.Create();
        var application = await SeedAsync(db);
        var service = new ApplicationSummaryService(db);

        var result = await service.GetSummaryAsync(application.Id);

        Assert.NotNull(result);
        Assert.Equal("Grand Towers", result!.PropertyName);
        Assert.Equal("101", result.UnitNumber);
        Assert.Equal("A B", result.FullName);
        Assert.Equal("1 Main St", result.CurrentAddress);
    }

    [Fact]
    public async Task GetSummaryAsync_OrdersResidenceHistoriesByMoveInDate()
    {
        await using var db = TestDb.Create();
        var application = await SeedAsync(db);
        var service = new ApplicationSummaryService(db);

        var result = await service.GetSummaryAsync(application.Id);

        Assert.Equal(new[] { "Older place", "Newer place" }, result!.ResidenceHistories.Select(r => r.Address));
    }

    [Fact]
    public async Task GetSummaryAsync_ForUnknownApplication_ReturnsNull()
    {
        await using var db = TestDb.Create();
        var service = new ApplicationSummaryService(db);

        var result = await service.GetSummaryAsync(999);

        Assert.Null(result);
    }
}
