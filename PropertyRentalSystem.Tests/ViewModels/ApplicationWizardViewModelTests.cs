using PropertyRentalSystem.Web.ViewModels.Applications;

namespace PropertyRentalSystem.Tests.ViewModels;

public class ApplicationWizardViewModelTests
{
    private static ApplicationWizardViewModel Valid() => new()
    {
        FullName = "A B",
        Phone = "123",
        Email = "a@b.com",
        CurrentAddress = "1 St"
    };

    [Fact]
    public void Validate_WithValidApplicantInfo_ReturnsNoErrors()
    {
        var results = ValidationTestHelper.Validate(Valid());

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenFullNameMissing_ReturnsErrorOnFullName()
    {
        var model = Valid();
        model.FullName = "";

        var results = ValidationTestHelper.Validate(model);

        Assert.True(results.HasErrorFor(nameof(ApplicationWizardViewModel.FullName)));
    }

    [Fact]
    public void Validate_WhenPhoneMissing_ReturnsErrorOnPhone()
    {
        var model = Valid();
        model.Phone = "";

        var results = ValidationTestHelper.Validate(model);

        Assert.True(results.HasErrorFor(nameof(ApplicationWizardViewModel.Phone)));
    }

    [Fact]
    public void Validate_WhenEmailMissing_ReturnsErrorOnEmail()
    {
        var model = Valid();
        model.Email = "";

        var results = ValidationTestHelper.Validate(model);

        Assert.True(results.HasErrorFor(nameof(ApplicationWizardViewModel.Email)));
    }

    [Fact]
    public void Validate_WhenEmailInvalid_ReturnsErrorOnEmail()
    {
        var model = Valid();
        model.Email = "not-an-email";

        var results = ValidationTestHelper.Validate(model);

        Assert.True(results.HasErrorFor(nameof(ApplicationWizardViewModel.Email)));
    }

    [Fact]
    public void Validate_WhenCurrentAddressMissing_ReturnsErrorOnCurrentAddress()
    {
        var model = Valid();
        model.CurrentAddress = "";

        var results = ValidationTestHelper.Validate(model);

        Assert.True(results.HasErrorFor(nameof(ApplicationWizardViewModel.CurrentAddress)));
    }
}
