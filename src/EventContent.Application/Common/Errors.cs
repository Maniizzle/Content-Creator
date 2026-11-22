using EventContent.Domain.Common;

namespace EventContent.Application.Common;

public static class CommonErrors
{
    public static readonly Error VersionConflict =
        Error.Conflict("common.version_conflict", "This item was changed by someone else. Reload and try again.");

    public static Error InvalidField(string field, string message) =>
        Error.Validation("common.invalid_request", "The request is invalid.", new Dictionary<string, string[]> { [field] = [message] });

    public static Error InvalidCursor => InvalidField("cursor", "The pagination cursor is invalid.");
}
