namespace PropertyRentalSystem.Web.Domain.Rules;

public static class LeaseRules
{
    // "A unit whose lease term covers today is not available."
    public static bool CoversDate(DateTime startDate, DateTime endDate, DateTime date) =>
        startDate <= date && endDate >= date;
}
