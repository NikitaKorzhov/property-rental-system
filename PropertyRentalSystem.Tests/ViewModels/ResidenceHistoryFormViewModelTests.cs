using PropertyRentalSystem.Web.ViewModels.Applications;

namespace PropertyRentalSystem.Tests.ViewModels;

public class ResidenceHistoryFormViewModelTests
{
    private static ResidenceHistoryFormViewModel Valid() => new()
    {
        RentalApplicationId = 1,
        Address = "1 Main St",
        LandlordName = "Jane Landlord",
        LandlordPhone = "+380501112233",
        MoveInDate = new DateTime(2023, 1, 1),
        MoveOutDate = new DateTime(2024, 1, 1)
    };

    [Fact]
    public void Validate_WithValidData_ReturnsNoErrors()
    {
        var results = ValidationTestHelper.Validate(Valid());

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenMoveOutBeforeMoveIn_ReturnsErrorOnMoveOutDate()
    {
        var model = Valid();
        model.MoveInDate = new DateTime(2024, 1, 1);
        model.MoveOutDate = new DateTime(2023, 1, 1);

        var results = ValidationTestHelper.Validate(model);

        Assert.True(results.HasErrorFor(nameof(ResidenceHistoryFormViewModel.MoveOutDate)));
    }

    [Fact]
    public void Validate_WhenMoveOutEqualsMoveIn_ReturnsNoError()
    {
        var model = Valid();
        model.MoveInDate = new DateTime(2024, 1, 1);
        model.MoveOutDate = new DateTime(2024, 1, 1);

        var results = ValidationTestHelper.Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenAddressMissing_ReturnsError()
    {
        var model = Valid();
        model.Address = "";

        var results = ValidationTestHelper.Validate(model);

        Assert.True(results.HasErrorFor(nameof(ResidenceHistoryFormViewModel.Address)));
    }
}
