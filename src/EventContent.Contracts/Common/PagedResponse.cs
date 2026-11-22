namespace EventContent.Contracts.Common;

/// <summary>Keyset-paginated list. Pass <see cref="NextCursor"/> back as <c>cursor</c> to get the next page.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
