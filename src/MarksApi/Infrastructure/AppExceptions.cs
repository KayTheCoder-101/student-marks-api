namespace MarksApi.Infrastructure;

/// <summary>Base class for expected, business-level failures. Mapped to problem details by GlobalExceptionHandler.</summary>
public abstract class AppException(string title, string? detail = null) : Exception(detail ?? title)
{
    public string Title { get; } = title;
    public string? Detail { get; } = detail;
    public abstract int StatusCode { get; }
}

public sealed class NotFoundException(string title, string? detail = null) : AppException(title, detail)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

public sealed class ConflictException(string title, string? detail = null) : AppException(title, detail)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
}

public sealed class ForbiddenException(string title, string? detail = null) : AppException(title, detail)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
}

public sealed class UnauthorizedAppException(string title, string? detail = null) : AppException(title, detail)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
}

public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : AppException("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
    public override int StatusCode => StatusCodes.Status400BadRequest;
}
