using System.ComponentModel.DataAnnotations;

namespace PropertyRentalSystem.Tests;

// Runs the exact same validation pipeline ASP.NET Core's model binder runs: DataAnnotations
// attributes plus IValidatableObject.Validate, in one pass.
public static class ValidationTestHelper
{
    public static IList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    public static bool HasErrorFor(this IList<ValidationResult> results, string memberName) =>
        results.Any(r => r.MemberNames.Contains(memberName));
}
