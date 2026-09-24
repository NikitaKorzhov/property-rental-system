using PropertyRentalSystem.Web.Domain.Rules;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Tests.Domain.Rules;

public class RentalApplicationRulesTests
{
    [Theory]
    [InlineData(ApplicationStatus.Draft, true)]
    [InlineData(ApplicationStatus.Returned, true)]
    [InlineData(ApplicationStatus.Submitted, false)]
    [InlineData(ApplicationStatus.Approved, false)]
    [InlineData(ApplicationStatus.Denied, false)]
    [InlineData(ApplicationStatus.Withdrawn, false)]
    public void IsEditable_MatchesSpec(ApplicationStatus status, bool expected)
    {
        Assert.Equal(expected, RentalApplicationRules.IsEditable(status));
    }

    [Theory]
    [InlineData(ApplicationStatus.Draft, true)]
    [InlineData(ApplicationStatus.Submitted, true)]
    [InlineData(ApplicationStatus.Returned, true)]
    [InlineData(ApplicationStatus.Approved, false)]
    [InlineData(ApplicationStatus.Denied, false)]
    [InlineData(ApplicationStatus.Withdrawn, false)]
    public void IsOpen_MatchesSpec(ApplicationStatus status, bool expected)
    {
        Assert.Equal(expected, RentalApplicationRules.IsOpen(status));
    }

    [Fact]
    public void CanSubmit_WhenBothSectionsComplete_ReturnsTrue()
    {
        Assert.True(RentalApplicationRules.CanSubmit(isApplicantInfoComplete: true, isResidenceHistoryComplete: true));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CanSubmit_WhenEitherSectionIncomplete_ReturnsFalse(bool infoComplete, bool residenceComplete)
    {
        Assert.False(RentalApplicationRules.CanSubmit(infoComplete, residenceComplete));
    }
}
