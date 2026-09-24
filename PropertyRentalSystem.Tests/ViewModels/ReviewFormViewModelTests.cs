using PropertyRentalSystem.Web.ViewModels.Review;

namespace PropertyRentalSystem.Tests.ViewModels;

public class ReviewFormViewModelTests
{
    [Fact]
    public void Validate_ApproveWithoutComment_ReturnsNoError()
    {
        var model = new ReviewFormViewModel { Id = 1, Outcome = ReviewOutcome.Approve, Comment = null };

        var results = ValidationTestHelper.Validate(model);

        Assert.Empty(results);
    }

    [Theory]
    [InlineData(ReviewOutcome.Return)]
    [InlineData(ReviewOutcome.Deny)]
    public void Validate_ReturnOrDenyWithoutComment_ReturnsErrorOnComment(ReviewOutcome outcome)
    {
        var model = new ReviewFormViewModel { Id = 1, Outcome = outcome, Comment = null };

        var results = ValidationTestHelper.Validate(model);

        Assert.True(results.HasErrorFor(nameof(ReviewFormViewModel.Comment)));
    }

    [Theory]
    [InlineData(ReviewOutcome.Return)]
    [InlineData(ReviewOutcome.Deny)]
    public void Validate_ReturnOrDenyWithWhitespaceOnlyComment_ReturnsError(ReviewOutcome outcome)
    {
        var model = new ReviewFormViewModel { Id = 1, Outcome = outcome, Comment = "   " };

        var results = ValidationTestHelper.Validate(model);

        Assert.True(results.HasErrorFor(nameof(ReviewFormViewModel.Comment)));
    }

    [Theory]
    [InlineData(ReviewOutcome.Return)]
    [InlineData(ReviewOutcome.Deny)]
    public void Validate_ReturnOrDenyWithComment_ReturnsNoError(ReviewOutcome outcome)
    {
        var model = new ReviewFormViewModel { Id = 1, Outcome = outcome, Comment = "Please provide more documents." };

        var results = ValidationTestHelper.Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WithoutOutcome_ReturnsError()
    {
        var model = new ReviewFormViewModel { Id = 1, Outcome = null };

        var results = ValidationTestHelper.Validate(model);

        Assert.True(results.HasErrorFor(nameof(ReviewFormViewModel.Outcome)));
    }
}
