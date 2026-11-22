using System.Diagnostics;
using System.Text.Json;
using EventContent.Application.Abstractions;
using EventContent.Domain.Audit;
using EventContent.Domain.Organizations;
using EventContent.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventContent.Infrastructure.Persistence;

internal sealed class OrganizationRepository(AppDbContext db) : IOrganizationRepository
{
    public Task<Organization?> GetWithMembersAsync(Guid organizationId, CancellationToken cancellationToken) =>
        db.Organizations
            .Include(o => o.Members)
            .AsSplitQuery()
            .SingleOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        db.Organizations.AnyAsync(o => o.Slug == slug, cancellationToken);

    public void Add(Organization organization) => db.Organizations.Add(organization);
}

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByIdentityAsync(string issuer, string subject, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.Issuer == issuer && u.Subject == subject, cancellationToken);

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        // E-mail is not an identity key; if several identities share it, pick the earliest deterministically.
        db.Users.Where(u => u.Email == email).OrderBy(u => u.CreatedAt).FirstOrDefaultAsync(cancellationToken);

    public async Task<User> ProvisionAsync(User candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        // ON CONFLICT makes concurrent first requests for the same identity safe without leaving a
        // failed insert tracked in the change tracker.
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO users (id, issuer, subject, email, display_name, created_at, last_seen_at)
            VALUES ({candidate.Id}, {candidate.Issuer}, {candidate.Subject}, {candidate.Email},
                    {candidate.DisplayName}, {candidate.CreatedAt}, {candidate.LastSeenAt})
            ON CONFLICT (issuer, subject) DO NOTHING
            """,
            cancellationToken);

        return await FindByIdentityAsync(candidate.Issuer, candidate.Subject, cancellationToken)
            ?? throw new InvalidOperationException("Provisioned user could not be loaded.");
    }
}

internal sealed class MembershipLookup(AppDbContext db) : IMembershipLookup
{
    public async Task<OrganizationRole?> FindRoleAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        // Runs before the tenant is resolved, so it must bypass the tenant filter explicitly.
        var member = await db.OrganizationMembers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.UserId == userId)
            .Select(m => new { m.Role })
            .SingleOrDefaultAsync(cancellationToken);
        return member?.Role;
    }
}

internal sealed class EfAuditLog(AppDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAuditLog
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Record(Guid organizationId, string entityType, Guid entityId, string action, object diff) =>
        db.AuditLogs.Add(AuditLogEntry.Create(
            organizationId,
            currentUser.UserId,
            entityType,
            entityId,
            action,
            JsonSerializer.Serialize(diff, SerializerOptions),
            Activity.Current?.TraceId.ToString(),
            clock.GetUtcNow()));
}

internal sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    private const string UniqueViolation = "23505";

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The row was modified or deleted since it was read.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation } pg)
        {
            throw new DuplicateKeyException(pg.ConstraintName ?? "unknown", ex);
        }
    }
}
