using System.Text;
using EventContent.Application.Abstractions;
using EventContent.Domain.Audit;
using EventContent.Domain.Common;
using EventContent.Domain.Organizations;
using EventContent.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EventContent.Infrastructure.Persistence;

/// <summary>
/// EF Core context. The schema itself is owned by the SQL migrations in <c>/db/migrations</c>
/// (see ADR 0005); this model only maps onto it.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    // Evaluated per query (EF parameterizes context members), so each request sees its own tenant.
    private bool TenantResolved => tenant.IsResolved;

    private Guid TenantId => tenant.IsResolved ? tenant.OrganizationId : Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Defense in depth: tenant-owned rows are invisible unless they belong to the resolved tenant.
        // With no resolved tenant the filter fails closed (returns nothing).
        var filtered = new HashSet<Type>();
        AddTenantFilter<OrganizationMember>(modelBuilder, filtered);
        AddTenantFilter<AuditLogEntry>(modelBuilder, filtered);

        ApplySnakeCaseNames(modelBuilder);
        EnsureTenantFilters(modelBuilder, filtered);
    }

    private void AddTenantFilter<T>(ModelBuilder modelBuilder, HashSet<Type> filtered)
        where T : class, ITenantScoped
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => TenantResolved && e.OrganizationId == TenantId);
        filtered.Add(typeof(T));
    }

    private static void EnsureTenantFilters(ModelBuilder modelBuilder, HashSet<Type> filtered)
    {
        var unfiltered = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType) && !filtered.Contains(t.ClrType))
            .Select(t => t.ClrType.Name)
            .ToList();

        if (unfiltered.Count > 0)
        {
            throw new InvalidOperationException(
                $"Tenant-scoped entities without a tenant query filter: {string.Join(", ", unfiltered)}.");
        }
    }

    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                // Respect explicit column names (e.g. the xmin concurrency token).
                if (property.FindAnnotation(RelationalAnnotationNames.ColumnName) is null)
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty));
            }
        }
    }

    internal static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1]))))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
