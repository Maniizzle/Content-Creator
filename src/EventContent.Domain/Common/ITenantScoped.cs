namespace EventContent.Domain.Common;

/// <summary>
/// Marks an entity that belongs to exactly one organization (tenant). Persistence applies a global
/// query filter to these entities so that a query can never read another tenant's rows by accident.
/// </summary>
public interface ITenantScoped
{
    Guid OrganizationId { get; }
}
