using EventContent.Application.Abstractions;
using EventContent.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace EventContent.Infrastructure.Persistence;

internal sealed class OrganizationQueries(AppDbContext db) : IOrganizationQueries
{
    public async Task<IReadOnlyList<(Organization Organization, OrganizationRole Role)>> ListForUserAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        // Cross-tenant by design: lists only organizations the user is a member of.
        var rows = await db.OrganizationMembers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(db.Organizations, m => m.OrganizationId, o => o.Id, (m, o) => new { Organization = o, m.Role })
            .OrderBy(x => x.Organization.Name)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => (r.Organization, r.Role))];
    }

    public async Task<IReadOnlyList<MemberView>> ListMembersAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var rows = await db.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.OrganizationId == organizationId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { Member = m, u.Email, u.DisplayName })
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new MemberView(r.Member, r.Email, r.DisplayName))];
    }

    public async Task<IReadOnlyList<AuditView>> ListAuditAsync(
        Guid organizationId, DateTimeOffset? before, Guid? beforeId, int take, CancellationToken cancellationToken)
    {
        // Keyset pagination uses a PostgreSQL row comparison, which LINQ cannot express for uuid.
        // FromSql is parameterized (interpolated values become SQL parameters), so this is injection-safe.
        var query = before is { } at && beforeId is { } id
            ? db.AuditLogs.FromSql($"SELECT * FROM audit_logs WHERE (occurred_at, id) < ({at}, {id})")
            : db.AuditLogs;

        query = query.AsNoTracking().Where(a => a.OrganizationId == organizationId);

        var rows = await query
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.Id)
            .Take(take)
            .GroupJoin(db.Users, a => a.ActorId, u => u.Id, (a, users) => new { a, users })
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, u) => new { Entry = x.a, ActorName = u != null ? u.DisplayName : null })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new AuditView(r.Entry, r.ActorName))];
    }
}
