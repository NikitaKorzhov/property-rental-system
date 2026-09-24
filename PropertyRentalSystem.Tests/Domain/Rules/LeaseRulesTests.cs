using PropertyRentalSystem.Web.Domain.Rules;

namespace PropertyRentalSystem.Tests.Domain.Rules;

public class LeaseRulesTests
{
    private static readonly DateTime Today = new(2026, 6, 15);

    [Fact]
    public void CoversDate_WhenTodayWithinRange_ReturnsTrue()
    {
        var result = LeaseRules.CoversDate(Today.AddDays(-10), Today.AddDays(10), Today);

        Assert.True(result);
    }

    [Fact]
    public void CoversDate_WhenTodayBeforeStart_ReturnsFalse()
    {
        var result = LeaseRules.CoversDate(Today.AddDays(1), Today.AddMonths(12), Today);

        Assert.False(result);
    }

    [Fact]
    public void CoversDate_WhenTodayAfterEnd_ReturnsFalse()
    {
        var result = LeaseRules.CoversDate(Today.AddMonths(-13), Today.AddDays(-1), Today);

        Assert.False(result);
    }

    [Fact]
    public void CoversDate_WhenTodayEqualsStartDate_ReturnsTrue()
    {
        var result = LeaseRules.CoversDate(Today, Today.AddMonths(12), Today);

        Assert.True(result);
    }

    [Fact]
    public void CoversDate_WhenTodayEqualsEndDate_ReturnsTrue()
    {
        var result = LeaseRules.CoversDate(Today.AddMonths(-12), Today, Today);

        Assert.True(result);
    }

    [Fact]
    public void ComputeEndDate_RunsThroughTheDayBeforeTheOneYearAnniversary()
    {
        var end = LeaseRules.ComputeEndDate(new DateTime(2026, 1, 1));

        Assert.Equal(new DateTime(2026, 12, 31), end);
    }

    [Fact]
    public void ComputeEndDate_DoesNotCoverTheStartDateOneYearLater()
    {
        var start = new DateTime(2026, 6, 15);
        var end = LeaseRules.ComputeEndDate(start);
        var oneYearLater = start.AddYears(1);

        // The unit must be available again exactly on the anniversary, not one day late.
        Assert.False(LeaseRules.CoversDate(start, end, oneYearLater));
    }
}
