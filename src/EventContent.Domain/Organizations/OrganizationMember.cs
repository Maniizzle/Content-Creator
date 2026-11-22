using EventContent.Domain.Common;

namespace EventContent.Domain.Organizations;

public sealed class OrganizationMember : ITenantScoped
{
    private OrganizationMember()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public OrganizationRole Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Optimistic concurrency token (mapped to PostgreSQL xmin).</summary>
    public uint Version { get; private set; }

    internal static OrganizationMember Create(Guid organizationId, Guid userId, OrganizationRole role, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
            CreatedAt = now,
        };

    internal void ChangeRole(OrganizationRole role) => Role = role;
}
