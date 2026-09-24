using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.Services.Applications;

namespace PropertyRentalSystem.Tests.Services.Applications;

public class ApplicationWizardServiceTests
{
    private static async Task<(Property property, Unit unit, RentalApplication application)> SeedAsync(
        ApplicationDbContext db, ApplicationStatus status = ApplicationStatus.Draft)
    {
        var property = new Property { Name = "P1", Address = "1 St" };
        var type = new UnitType { Name = "Studio", IsActive = true };
        db.Properties.Add(property);
        db.UnitTypes.Add(type);
        await db.SaveChangesAsync();
        var unit = new Unit { PropertyId = property.Id, UnitTypeId = type.Id, UnitNumber = "101" };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        var application = new RentalApplication { UnitId = unit.Id, ApplicantId = "applicant-1", Status = status };
        db.RentalApplications.Add(application);
        await db.SaveChangesAsync();
        return (property, unit, application);
    }

    // ---------- DetermineStep ----------

    [Fact]
    public void DetermineStep_NotEditable_ReturnsSummary()
    {
        using var db = TestDb.Create();
        var service = new ApplicationWizardService(db);
        var application = new RentalApplication { Status = ApplicationStatus.Submitted };

        Assert.Equal(WizardStep.Summary, service.DetermineStep(application));
    }

    [Fact]
    public void DetermineStep_EditableAndNothingComplete_ReturnsApplicantInfo()
    {
        using var db = TestDb.Create();
        var service = new ApplicationWizardService(db);
        var application = new RentalApplication { Status = ApplicationStatus.Draft };

        Assert.Equal(WizardStep.ApplicantInfo, service.DetermineStep(application));
    }

    [Fact]
    public void DetermineStep_ApplicantInfoDoneOnly_ReturnsResidenceHistory()
    {
        using var db = TestDb.Create();
        var service = new ApplicationWizardService(db);
        var application = new RentalApplication { Status = ApplicationStatus.Draft, IsApplicantInfoComplete = true };

        Assert.Equal(WizardStep.ResidenceHistory, service.DetermineStep(application));
    }

    [Fact]
    public void DetermineStep_BothSectionsCompleteAndEditable_ReturnsApplicantInfoNotSummary()
    {
        using var db = TestDb.Create();
        var service = new ApplicationWizardService(db);
        var application = new RentalApplication
        {
            Status = ApplicationStatus.Returned,
            IsApplicantInfoComplete = true,
            IsResidenceHistoryComplete = true
        };

        Assert.Equal(WizardStep.ApplicantInfo, service.DetermineStep(application));
    }

    // ---------- GoBack ----------

    [Theory]
    [InlineData(WizardStep.ResidenceHistory, WizardStep.ApplicantInfo)]
    [InlineData(WizardStep.Summary, WizardStep.ResidenceHistory)]
    [InlineData(WizardStep.ApplicantInfo, WizardStep.ApplicantInfo)]
    public void GoBack_MovesToThePreviousStep(WizardStep current, WizardStep expected)
    {
        using var db = TestDb.Create();
        var service = new ApplicationWizardService(db);

        Assert.Equal(expected, service.GoBack(current));
    }

    // ---------- SaveApplicantInfoAsync ----------

    [Fact]
    public async Task SaveApplicantInfoAsync_WithAllFieldsMissing_ReturnsAllFourFieldErrors()
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db);
        var service = new ApplicationWizardService(db);

        var result = await service.SaveApplicantInfoAsync(application, "", "", "", "");

        Assert.False(result.Succeeded);
        Assert.Equal(4, result.Errors.Count);
    }

    [Fact]
    public async Task SaveApplicantInfoAsync_WithInvalidEmail_FailsOnEmailField()
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db);
        var service = new ApplicationWizardService(db);

        var result = await service.SaveApplicantInfoAsync(application, "A B", "123", "not-an-email", "1 St");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Field == "Email");
    }

    [Fact]
    public async Task SaveApplicantInfoAsync_WhenNotEditable_Fails()
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db, ApplicationStatus.Submitted);
        var service = new ApplicationWizardService(db);

        var result = await service.SaveApplicantInfoAsync(application, "A B", "123", "a@b.com", "1 St");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SaveApplicantInfoAsync_TrimsWhitespaceAndMarksSectionComplete()
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db);
        var service = new ApplicationWizardService(db);

        var result = await service.SaveApplicantInfoAsync(application, "  A B  ", " 123 ", " a@b.com ", " 1 St ");

        Assert.True(result.Succeeded);
        Assert.Equal("A B", application.FullName);
        Assert.True(application.IsApplicantInfoComplete);
    }

    // ---------- SubmitAsync ----------

    [Fact]
    public async Task SubmitAsync_WhenSectionsIncomplete_Fails()
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db);
        var service = new ApplicationWizardService(db);

        var result = await service.SubmitAsync(application, "applicant-1");

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationStatus.Draft, application.Status);
    }

    [Fact]
    public async Task SubmitAsync_WhenUnitHasActiveLease_Fails()
    {
        await using var db = TestDb.Create();
        var (_, unit, application) = await SeedAsync(db);
        application.IsApplicantInfoComplete = true;
        application.IsResidenceHistoryComplete = true;
        var today = DateTime.UtcNow.Date;
        db.Leases.Add(new Lease { UnitId = unit.Id, StartDate = today.AddDays(-1), EndDate = today.AddDays(1) });
        await db.SaveChangesAsync();
        var service = new ApplicationWizardService(db);

        var result = await service.SubmitAsync(application, "applicant-1");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SubmitAsync_WhenComplete_TransitionsToSubmittedAndRecordsHistory()
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db);
        application.IsApplicantInfoComplete = true;
        application.IsResidenceHistoryComplete = true;
        await db.SaveChangesAsync();
        var service = new ApplicationWizardService(db);

        var result = await service.SubmitAsync(application, "applicant-1");

        Assert.True(result.Succeeded);
        Assert.Equal(ApplicationStatus.Submitted, application.Status);
        Assert.Single(db.ApplicationStatusHistories);
    }

    // ---------- WithdrawAsync ----------

    [Theory]
    [InlineData(ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.Denied)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public async Task WithdrawAsync_WhenStatusIsTerminal_Fails(ApplicationStatus status)
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db, status);
        var service = new ApplicationWizardService(db);

        var result = await service.WithdrawAsync(application, "applicant-1");

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(ApplicationStatus.Draft)]
    [InlineData(ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Returned)]
    public async Task WithdrawAsync_WhenStatusIsOpen_Succeeds(ApplicationStatus status)
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db, status);
        var service = new ApplicationWizardService(db);

        var result = await service.WithdrawAsync(application, "applicant-1");

        Assert.True(result.Succeeded);
        Assert.Equal(ApplicationStatus.Withdrawn, application.Status);
    }
}
