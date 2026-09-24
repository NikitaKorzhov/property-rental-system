namespace PropertyRentalSystem.Web.Services;

// Field is the ViewModel property name a controller should attach this to via
// ModelState.AddModelError, or "" for a form-level (non-field-specific) error.
public sealed record ServiceError(string Field, string Message);

// A business-rule outcome that isn't an HTTP concern (unlike 404/400) and isn't a validation
// shape concern (unlike DataAnnotations on a view model) — services return this instead of
// throwing, so a controller can turn it into ModelState errors without try/catch.
public class ServiceResult
{
    public bool Succeeded { get; }
    public IReadOnlyList<ServiceError> Errors { get; }

    protected ServiceResult(bool succeeded, IReadOnlyList<ServiceError> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public static ServiceResult Success() => new(true, Array.Empty<ServiceError>());
    public static ServiceResult Fail(string message) => new(false, new[] { new ServiceError(string.Empty, message) });
    public static ServiceResult Fail(string field, string message) => new(false, new[] { new ServiceError(field, message) });
    public static ServiceResult Fail(IReadOnlyList<ServiceError> errors) => new(false, errors);
}

public sealed class ServiceResult<T> : ServiceResult
{
    public T? Value { get; }

    private ServiceResult(bool succeeded, T? value, IReadOnlyList<ServiceError> errors) : base(succeeded, errors)
    {
        Value = value;
    }

    public static ServiceResult<T> Success(T value) => new(true, value, Array.Empty<ServiceError>());
    public static new ServiceResult<T> Fail(string message) => new(false, default, new[] { new ServiceError(string.Empty, message) });
    public static new ServiceResult<T> Fail(string field, string message) => new(false, default, new[] { new ServiceError(field, message) });
    public static new ServiceResult<T> Fail(IReadOnlyList<ServiceError> errors) => new(false, default, errors);
}
