using System.Globalization;
using System.Text;
using EventContent.Application.Abstractions;
using EventContent.Application.Common;
using EventContent.Contracts.Audit;
using EventContent.Contracts.Common;
using EventContent.Domain.Common;

namespace EventContent.Application.Audit;

public sealed class ListAuditLogHandler(IOrganizationQueries queries, ITenantContext tenant)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public async Task<Result<PagedResponse<AuditLogEntryResponse>>> HandleAsync(
        string? cursor, int? pageSize, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        DateTimeOffset? before = null;
        Guid? beforeId = null;

        if (!string.IsNullOrEmpty(cursor))
        {
            if (!AuditCursor.TryDecode(cursor, out var occurredAt, out var id))
            {
                return CommonErrors.InvalidCursor;
            }

            (before, beforeId) = (occurredAt, id);
        }

        // Fetch one extra row to know whether another page exists.
        var rows = await queries.ListAuditAsync(tenant.OrganizationId, before, beforeId, take + 1, cancellationToken);
        var page = rows.Take(take).ToList();
        var next = rows.Count > take ? AuditCursor.Encode(page[^1].Entry.OccurredAt, page[^1].Entry.Id) : null;

        return new PagedResponse<AuditLogEntryResponse>(
            [.. page.Select(r => new AuditLogEntryResponse(
                r.Entry.Id,
                r.Entry.ActorId,
                r.ActorName,
                r.Entry.EntityType,
                r.Entry.EntityId,
                r.Entry.Action,
                r.Entry.DiffJson,
                r.Entry.OccurredAt))],
            next);
    }
}

/// <summary>Opaque keyset cursor: (occurredAt, id) encoded as base64url.</summary>
internal static class AuditCursor
{
    public static string Encode(DateTimeOffset occurredAt, Guid id)
    {
        var raw = string.Create(CultureInfo.InvariantCulture, $"{occurredAt.UtcTicks}:{id:N}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryDecode(string cursor, out DateTimeOffset occurredAt, out Guid id)
    {
        occurredAt = default;
        id = default;
        if (cursor.Length > 128)
        {
            return false;
        }

        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split(':');
            if (parts.Length != 2
                || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                || ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks
                || !Guid.TryParseExact(parts[1], "N", out id))
            {
                return false;
            }

            occurredAt = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
