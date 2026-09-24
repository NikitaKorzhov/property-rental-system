namespace PropertyRentalSystem.Web.Domain.Rules;

public static class LeaseRules
{
    // "A unit whose lease term covers today is not available."
    public static bool CoversDate(DateTime startDate, DateTime endDate, DateTime date) =>
        startDate <= date && endDate >= date;

    // A twelve-month lease runs through the day before its one-year anniversary (e.g.
    // Jan 1 – Dec 31), not through the anniversary date itself — otherwise the term would
    // run 12 months plus one extra day.
    public static DateTime ComputeEndDate(DateTime startDate) => startDate.AddMonths(12).AddDays(-1);
}
