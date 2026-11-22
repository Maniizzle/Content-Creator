using EventContent.Domain.Audit;
using EventContent.Domain.Organizations;
using EventContent.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventContent.Infrastructure.Persistence;

internal static class ConfigurationExtensions
{
    /// <summary>Maps a <c>uint Version</c> property onto PostgreSQL's <c>xmin</c> system column.</summary>
    public static void HasXminVersion<T>(this EntityTypeBuilder<T> builder)
        where T : class =>
        builder.Property<uint>("Version")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.HasIndex(u => new { u.Issuer, u.Subject }).IsUnique().HasDatabaseName("uq_users_identity");
        builder.HasIndex(u => u.Email).HasDatabaseName("ix_users_email");
    }
}

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.HasIndex(o => o.Slug).IsUnique().HasDatabaseName("uq_organizations_slug");
        builder.HasXminVersion();

        builder.HasMany(o => o.Members)
            .WithOne()
            .HasForeignKey(m => m.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("organization_members");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Role).HasConversion<string>();
        builder.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique().HasDatabaseName("uq_organization_members_user");
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasXminVersion();
    }
}

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.DiffJson).HasColumnType("jsonb");
        builder.HasOne<Organization>().WithMany().HasForeignKey(a => a.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
