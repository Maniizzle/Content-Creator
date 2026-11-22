using EventContent.Domain.Audit;
using EventContent.Domain.Organizations;
using EventContent.Domain.Users;

namespace EventContent.Application.Abstractions;

/// <summary>The authenticated, provisioned application user for the current request.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }

    string Email { get; }

    string DisplayName { get; }
}

/// <summary>
/// The organization (tenant) the current request operates in, resolved and membership-checked
/// server-side before any handler runs.
/// </summary>
public interface ITenantContext
{
    bool IsResolved { get; }

    Guid OrganizationId { get; }

    OrganizationRole Role { get; }
}

public interface IOrganizationRepository
{
    /// <summary>Loads an organization with all members. Returns null if it does not exist.</summary>
    Task<Organization?> GetWithMembersAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    void Add(Organization organization);
}

public interface IUserRepository
{
    Task<User?> FindByIdentityAsync(string issuer, string subject, CancellationToken cancellationToken);

    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts <paramref name="candidate"/> unless a user with the same identity already exists
    /// (race-safe), then returns the stored, tracked user.
    /// </summary>
    Task<User> ProvisionAsync(User candidate, CancellationToken cancellationToken);
}

public interface IMembershipLookup
{
    /// <summary>Returns the user's role in the organization, or null if the user is not a member.</summary>
    Task<OrganizationRole?> FindRoleAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
}

public interface IAuditLog
{
    /// <summary>
    /// Stages an audit entry in the current unit of work, so it commits atomically with the change.
    /// <paramref name="diff"/> is serialized to JSON and must not contain secrets.
    /// </summary>
    void Record(Guid organizationId, string entityType, Guid entityId, string action, object diff);
}

public interface IUnitOfWork
{
    /// <exception cref="ConcurrencyConflictException">The row changed since it was read.</exception>
    /// <exception cref="DuplicateKeyException">A unique constraint was violated.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ConcurrencyConflictException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class DuplicateKeyException(string constraintName, Exception? inner = null)
    : Exception($"Unique constraint '{constraintName}' was violated.", inner)
{
    public string ConstraintName { get; } = constraintName;
}

/// <summary>Read-only projections for list/detail screens.</summary>
public interface IOrganizationQueries
{
    Task<IReadOnlyList<(Organization Organization, OrganizationRole Role)>> ListForUserAsync(
        Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberView>> ListMembersAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditView>> ListAuditAsync(
        Guid organizationId, DateTimeOffset? before, Guid? beforeId, int take, CancellationToken cancellationToken);
}

public sealed record MemberView(OrganizationMember Member, string Email, string DisplayName);

public sealed record AuditView(AuditLogEntry Entry, string? ActorName);
