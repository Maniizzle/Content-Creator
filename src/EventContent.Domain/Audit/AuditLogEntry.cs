using EventContent.Domain.Common;

namespace EventContent.Domain.Audit;

/// <summary>Append-only record of a security- or content-relevant change.</summary>
public sealed class AuditLogEntry : ITenantScoped
{
    private AuditLogEntry()
    {
        EntityType = string.Empty;
        Action = string.Empty;
        DiffJson = "{}";
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid? ActorId { get; private set; }

    public string EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    public string Action { get; private set; }

    /// <summary>JSON describing the change. Must never contain secrets, tokens or signed URLs.</summary>
    public string DiffJson { get; private set; }

    public string? TraceId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditLogEntry Create(
        Guid organizationId,
        Guid? actorId,
        string entityType,
        Guid entityId,
        string action,
        string diffJson,
        string? traceId,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            OrganizationId = organizationId,
            ActorId = actorId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            DiffJson = diffJson,
            TraceId = traceId,
            OccurredAt = now,
        };
}
