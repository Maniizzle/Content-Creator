namespace EventContent.Domain.Common;

/// <summary>Category of a failure; the API layer maps it to an HTTP status code.</summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized,
}

/// <summary>
/// A stable, machine-readable failure. <see cref="Code"/> values are part of the public API contract
/// and must not change once released.
/// </summary>
public sealed record Error(string Code, string Message, ErrorKind Kind)
{
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; init; }

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? fieldErrors = null) =>
        new(code, message, ErrorKind.Validation) { FieldErrors = fieldErrors };

    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden);
}
