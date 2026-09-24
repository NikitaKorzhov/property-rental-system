using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.Services.Review;

namespace PropertyRentalSystem.Tests.Services.Review;

public class ApplicationReviewServiceTests
{
    // GetFilteredAsync/GetHistoryAsync Include a required Applicant/ChangedBy navigation, so
    // the referenced user has to actually exist — the in-memory provider (unlike a real FK-
    // enforcing database) would otherwise silently drop rows whose required nav can't resolve.
    private static async Task<ApplicationUser> SeedUserAsync(ApplicationDbContext db, string id)
    {
        var user = new ApplicationUser { Id = id, UserName = $"{id}@test.com", Email = $"{id}@test.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<(Property property, Unit unit, RentalApplication application)> SeedAsync(
        ApplicationDbContext db, ApplicationStatus status = ApplicationStatus.Submitted)
    {
        var applicant = await SeedUserAsync(db, "applicant-1");
        var property = new Property { Name = "P1", Address = "1 St" };
        var type = new UnitType { Name = "Studio", IsActive = true };
        db.Properties.Add(property);
        db.UnitTypes.Add(type);
        await db.SaveChangesAsync();
        var unit = new Unit { PropertyId = property.Id, UnitTypeId = type.Id, UnitNumber = "101" };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        var application = new RentalApplication { UnitId = unit.Id, ApplicantId = applicant.Id, Status = status };
        db.RentalApplications.Add(application);
        await db.SaveChangesAsync();
        return (property, unit, application);
    }

    [Fact]
    public async Task GetFilteredAsync_ByStatus_OnlyReturnsMatchingApplications()
    {
        await using var db = TestDb.Create();
        var (_, unit, _) = await SeedAsync(db, ApplicationStatus.Submitted);
        var applicant2 = await SeedUserAsync(db, "applicant-2");
        db.RentalApplications.Add(new RentalApplication { UnitId = unit.Id, ApplicantId = applicant2.Id, Status = ApplicationStatus.Draft });
        await db.SaveChangesAsync();
        var service = new ApplicationReviewService(db);

        var result = await service.GetFilteredAsync(ApplicationStatus.Draft, null);

        Assert.Single(result);
        Assert.Equal(ApplicationStatus.Draft, result[0].Status);
    }

    [Fact]
    public async Task GetFilteredAsync_ByProperty_OnlyReturnsApplicationsForThatProperty()
    {
        await using var db = TestDb.Create();
        var (property1, _, application1) = await SeedAsync(db);
        var property2 = new Property { Name = "P2", Address = "2 St" };
        db.Properties.Add(property2);
        await db.SaveChangesAsync();
        var unit2 = new Unit { PropertyId = property2.Id, UnitTypeId = 1, UnitNumber = "201" };
        db.Units.Add(unit2);
        await db.SaveChangesAsync();
        var applicant2 = await SeedUserAsync(db, "applicant-2");
        db.RentalApplications.Add(new RentalApplication { UnitId = unit2.Id, ApplicantId = applicant2.Id, Status = ApplicationStatus.Submitted });
        await db.SaveChangesAsync();
        var service = new ApplicationReviewService(db);

        var result = await service.GetFilteredAsync(null, property1.Id);

        Assert.Single(result);
        Assert.Equal(application1.Id, result[0].Id);
    }

    [Theory]
    [InlineData(ApplicationStatus.Submitted, true)]
    [InlineData(ApplicationStatus.Draft, false)]
    [InlineData(ApplicationStatus.Approved, false)]
    public async Task GetReviewableAsync_OnlySubmittedIsReviewable(ApplicationStatus status, bool expectFound)
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db, status);
        var service = new ApplicationReviewService(db);

        var result = await service.GetReviewableAsync(application.Id);

        Assert.Equal(expectFound, result != null);
    }

    [Fact]
    public async Task ReviewAsync_WhenApplicationNoLongerSubmitted_Fails()
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db, ApplicationStatus.Approved);
        var service = new ApplicationReviewService(db);

        var result = await service.ReviewAsync(application, ReviewOutcome.Deny, "too late", "manager-1");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ReviewAsync_ApproveWhenUnitAlreadyHasActiveLease_FailsAndPreventsASecondLease()
    {
        await using var db = TestDb.Create();
        var (_, unit, application) = await SeedAsync(db);
        var today = DateTime.UtcNow.Date;
        db.Leases.Add(new Lease { UnitId = unit.Id, StartDate = today.AddDays(-1), EndDate = today.AddDays(30) });
        await db.SaveChangesAsync();
        var service = new ApplicationReviewService(db);

        var result = await service.ReviewAsync(application, ReviewOutcome.Approve, null, "manager-1");

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationStatus.Submitted, application.Status);
        Assert.Single(db.Leases); // still just the pre-existing one
    }

    [Fact]
    public async Task ReviewAsync_ApproveWhenUnitIsFree_CreatesA364DayLeaseAndApprovesTheApplication()
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db);
        var service = new ApplicationReviewService(db);
        var today = DateTime.UtcNow.Date;

        var result = await service.ReviewAsync(application, ReviewOutcome.Approve, null, "manager-1");

        Assert.True(result.Succeeded);
        Assert.Equal(ApplicationStatus.Approved, application.Status);
        var lease = Assert.Single(db.Leases);
        Assert.Equal(today, lease.StartDate);
        Assert.Equal(today.AddMonths(12).AddDays(-1), lease.EndDate);
    }

    [Theory]
    [InlineData(ReviewOutcome.Return, ApplicationStatus.Returned)]
    [InlineData(ReviewOutcome.Deny, ApplicationStatus.Denied)]
    public async Task ReviewAsync_ReturnOrDeny_SetsTheCorrespondingStatusAndRecordsTheComment(
        ReviewOutcome outcome, ApplicationStatus expectedStatus)
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db);
        var service = new ApplicationReviewService(db);

        var result = await service.ReviewAsync(application, outcome, "Please fix your phone number.", "manager-1");

        Assert.True(result.Succeeded);
        Assert.Equal(expectedStatus, application.Status);
        var history = Assert.Single(db.ApplicationStatusHistories);
        Assert.Equal("Please fix your phone number.", history.Comment);
        Assert.Equal("manager-1", history.ChangedByUserId);
    }

    [Fact]
    public async Task ReviewAsync_WithWhitespaceOnlyComment_StoresNullNotBlank()
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db);
        var service = new ApplicationReviewService(db);

        await service.ReviewAsync(application, ReviewOutcome.Approve, "   ", "manager-1");

        var history = Assert.Single(db.ApplicationStatusHistories);
        Assert.Null(history.Comment);
    }
}
