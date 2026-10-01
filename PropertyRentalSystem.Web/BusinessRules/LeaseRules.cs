using System.Linq.Expressions;
using PropertyRentalSystem.Web.Models;

namespace PropertyRentalSystem.Web.BusinessRules;

public static class LeaseRules
{
    // "A unit whose lease term covers today is not available."
    public static bool CoversDate(DateTime startDate, DateTime endDate, DateTime date) =>
        startDate <= date && endDate >= date;

    // Same rule as CoversDate, as an expression tree EF Core can translate into SQL — a plain
    // method call to CoversDate inside a query can't be. LeaseRulesTests asserts they agree.
    public static Expression<Func<Lease, bool>> IsActiveOn(DateTime date) =>
        l => l.StartDate <= date && l.EndDate >= date;

    // A twelve-month lease runs through the day before its one-year anniversary (e.g.
    // Jan 1 – Dec 31), not through the anniversary date itself — otherwise the term would
    // run 12 months plus one extra day.
    public static DateTime ComputeEndDate(DateTime startDate) => startDate.AddMonths(12).AddDays(-1);
}
