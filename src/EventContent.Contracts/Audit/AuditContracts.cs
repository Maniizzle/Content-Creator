namespace EventContent.Contracts.Audit;

public sealed record AuditLogEntryResponse(
    Guid Id,
    Guid? ActorId,
    string? ActorName,
    string EntityType,
    Guid EntityId,
    string Action,
    string Diff,
    DateTimeOffset OccurredAt);
