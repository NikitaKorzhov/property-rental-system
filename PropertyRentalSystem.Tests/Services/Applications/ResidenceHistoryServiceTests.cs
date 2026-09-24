using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.Services.Applications;

namespace PropertyRentalSystem.Tests.Services.Applications;

public class ResidenceHistoryServiceTests
{
    private static async Task<RentalApplication> SeedApplicationAsync(
        ApplicationDbContext db, string applicantId, ApplicationStatus status)
    {
        var property = new Property { Name = "P1", Address = "1 St" };
        var type = new UnitType { Name = "Studio", IsActive = true };
        db.Properties.Add(property);
        db.UnitTypes.Add(type);
        await db.SaveChangesAsync();
        var unit = new Unit { PropertyId = property.Id, UnitTypeId = type.Id, UnitNumber = "101" };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        var application = new RentalApplication { UnitId = unit.Id, ApplicantId = applicantId, Status = status };
        db.RentalApplications.Add(application);
        await db.SaveChangesAsync();
        return application;
    }

    [Fact]
    public async Task GetOwnedEditableApplicationAsync_WhenOwnedAndEditable_ReturnsIt()
    {
        await using var db = TestDb.Create();
        var application = await SeedApplicationAsync(db, "applicant-1", ApplicationStatus.Draft);
        var service = new ResidenceHistoryService(db);

        var result = await service.GetOwnedEditableApplicationAsync(application.Id, "applicant-1");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetOwnedEditableApplicationAsync_WhenOwnedByAnotherApplicant_ReturnsNull()
    {
        await using var db = TestDb.Create();
        var application = await SeedApplicationAsync(db, "applicant-1", ApplicationStatus.Draft);
        var service = new ResidenceHistoryService(db);

        var result = await service.GetOwnedEditableApplicationAsync(application.Id, "someone-else");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOwnedEditableApplicationAsync_WhenSubmittedAndNoLongerEditable_ReturnsNull()
    {
        await using var db = TestDb.Create();
        var application = await SeedApplicationAsync(db, "applicant-1", ApplicationStatus.Submitted);
        var service = new ResidenceHistoryService(db);

        var result = await service.GetOwnedEditableApplicationAsync(application.Id, "applicant-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOwnedEditableResidenceAsync_WhenParentApplicationIsEditable_ReturnsIt()
    {
        await using var db = TestDb.Create();
        var application = await SeedApplicationAsync(db, "applicant-1", ApplicationStatus.Returned);
        var service = new ResidenceHistoryService(db);
        var residence = await service.AddAsync(
            application.Id, "1 Old St", "Landlord", "555", DateTime.Today.AddYears(-1), DateTime.Today);

        var result = await service.GetOwnedEditableResidenceAsync(residence.Id, "applicant-1");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task AddAsync_PersistsTheResidence()
    {
        await using var db = TestDb.Create();
        var application = await SeedApplicationAsync(db, "applicant-1", ApplicationStatus.Draft);
        var service = new ResidenceHistoryService(db);

        await service.AddAsync(application.Id, "1 Old St", "Landlord", "555", DateTime.Today.AddYears(-1), DateTime.Today);

        Assert.Single(db.ResidenceHistories);
    }

    [Fact]
    public async Task UpdateAsync_OverwritesFields()
    {
        await using var db = TestDb.Create();
        var application = await SeedApplicationAsync(db, "applicant-1", ApplicationStatus.Draft);
        var service = new ResidenceHistoryService(db);
        var residence = await service.AddAsync(
            application.Id, "1 Old St", "Landlord", "555", DateTime.Today.AddYears(-1), DateTime.Today);

        await service.UpdateAsync(residence, "2 New St", "New Landlord", "999", DateTime.Today.AddYears(-2), DateTime.Today.AddYears(-1));

        Assert.Equal("2 New St", residence.Address);
        Assert.Equal("New Landlord", residence.LandlordName);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheResidence()
    {
        await using var db = TestDb.Create();
        var application = await SeedApplicationAsync(db, "applicant-1", ApplicationStatus.Draft);
        var service = new ResidenceHistoryService(db);
        var residence = await service.AddAsync(
            application.Id, "1 Old St", "Landlord", "555", DateTime.Today.AddYears(-1), DateTime.Today);

        await service.DeleteAsync(residence);

        Assert.Empty(db.ResidenceHistories);
    }
}
