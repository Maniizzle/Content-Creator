using EventContent.Application.Abstractions;
using EventContent.Application.Audit;
using EventContent.Application.Common;
using EventContent.Application.Organizations;
using EventContent.Contracts.Organizations;
using EventContent.Domain.Organizations;
using EventContent.Domain.Users;

namespace EventContent.UnitTests.Application;

public sealed class OrganizationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private readonly Fakes _fakes = new();

    [Fact]
    public async Task Create_persists_and_audits()
    {
        var handler = new CreateOrganizationHandler(_fakes, _fakes, _fakes, _fakes, new FixedClock(Now));

        var result = await handler.HandleAsync(new CreateOrganizationRequest("JCI Lagos", "jci-lagos", "Africa/Lagos"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Owner", result.Value.MyRole);
        Assert.Contains("MembersManage", result.Value.MyPermissions);
        Assert.Single(_fakes.Organizations);
        Assert.Equal("organization.created", Assert.Single(_fakes.AuditActions));
        Assert.Equal(1, _fakes.SaveCount);
    }

    [Fact]
    public async Task Create_rejects_a_taken_slug_without_saving()
    {
        _fakes.Organizations.Add(Organization.Create("Existing", "jci-lagos", "UTC", Guid.NewGuid(), Now).Value);
        var handler = new CreateOrganizationHandler(_fakes, _fakes, _fakes, _fakes, new FixedClock(Now));

        var result = await handler.HandleAsync(new CreateOrganizationRequest("JCI Lagos", "jci-lagos", "Africa/Lagos"), CancellationToken.None);

        Assert.Equal(OrganizationErrors.SlugTaken, result.Error);
        Assert.Equal(0, _fakes.SaveCount);
    }

    [Fact]
    public async Task Create_maps_a_unique_violation_race_to_slug_taken()
    {
        _fakes.ThrowOnSave = new DuplicateKeyException("uq_organizations_slug");
        var handler = new CreateOrganizationHandler(_fakes, _fakes, _fakes, _fakes, new FixedClock(Now));

        var result = await handler.HandleAsync(new CreateOrganizationRequest("JCI Lagos", "jci-lagos", "Africa/Lagos"), CancellationToken.None);

        Assert.Equal(OrganizationErrors.SlugTaken, result.Error);
    }

    [Fact]
    public async Task Update_with_a_stale_version_is_a_conflict()
    {
        var organization = _fakes.SeedTenant(OrganizationRole.Owner, Now);
        var handler = new UpdateOrganizationHandler(_fakes, _fakes, _fakes, _fakes);

        var result = await handler.HandleAsync(new UpdateOrganizationRequest("New name", "UTC", organization.Version + 1), CancellationToken.None);

        Assert.Equal(CommonErrors.VersionConflict, result.Error);
        Assert.Equal(0, _fakes.SaveCount);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("Owner,Admin")]
    [InlineData("superuser")]
    [InlineData("")]
    public async Task AddMember_rejects_roles_that_are_not_exact_names(string role)
    {
        _fakes.SeedTenant(OrganizationRole.Owner, Now);
        var handler = new AddMemberHandler(_fakes, _fakes, _fakes, _fakes, _fakes, new FixedClock(Now));

        var result = await handler.HandleAsync(new AddMemberRequest("someone@example.com", role), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("common.invalid_request", result.Error.Code);
    }

    [Fact]
    public async Task AddMember_requires_an_existing_user()
    {
        _fakes.SeedTenant(OrganizationRole.Owner, Now);
        var handler = new AddMemberHandler(_fakes, _fakes, _fakes, _fakes, _fakes, new FixedClock(Now));

        var result = await handler.HandleAsync(new AddMemberRequest("nobody@example.com", "Editor"), CancellationToken.None);

        Assert.Equal(OrganizationErrors.UserNotFound, result.Error);
    }

    [Fact]
    public async Task AddMember_matches_email_case_insensitively_and_audits()
    {
        _fakes.SeedTenant(OrganizationRole.Admin, Now);
        _fakes.Users.Add(User.Provision("iss", "sub-2", "editor@example.com", "Ed", Now));
        var handler = new AddMemberHandler(_fakes, _fakes, _fakes, _fakes, _fakes, new FixedClock(Now));

        var result = await handler.HandleAsync(new AddMemberRequest(" Editor@Example.COM ", "editor"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Editor", result.Value.Role);
        Assert.Contains("member.added", _fakes.AuditActions);
    }

    [Fact]
    public void Audit_cursor_round_trips_and_rejects_garbage()
    {
        var id = Guid.CreateVersion7(Now);
        var cursor = AuditCursor.Encode(Now, id);

        Assert.True(AuditCursor.TryDecode(cursor, out var at, out var decodedId));
        Assert.Equal(Now, at);
        Assert.Equal(id, decodedId);
        Assert.False(AuditCursor.TryDecode("not-a-cursor!", out _, out _));
        Assert.False(AuditCursor.TryDecode(new string('a', 200), out _, out _));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>In-memory implementations of the application ports.</summary>
    private sealed class Fakes : IOrganizationRepository, IUserRepository, ICurrentUser, ITenantContext, IAuditLog, IUnitOfWork
    {
        private Organization? _tenantOrganization;
        private OrganizationRole _tenantRole;

        public List<Organization> Organizations { get; } = [];

        public List<User> Users { get; } = [];

        public List<string> AuditActions { get; } = [];

        public int SaveCount { get; private set; }

        public Exception? ThrowOnSave { get; set; }

        public Guid UserId { get; } = Guid.NewGuid();

        public string Email => "owner@example.com";

        public string DisplayName => "Owner";

        public bool IsResolved => _tenantOrganization is not null;

        public Guid OrganizationId => _tenantOrganization!.Id;

        public OrganizationRole Role => _tenantRole;

        public Organization SeedTenant(OrganizationRole role, DateTimeOffset now)
        {
            var organization = Organization.Create("Tenant", "tenant-org", "UTC", UserId, now).Value;
            Organizations.Add(organization);
            _tenantOrganization = organization;
            _tenantRole = role;
            return organization;
        }

        public Task<Organization?> GetWithMembersAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(Organizations.SingleOrDefault(o => o.Id == organizationId));

        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(Organizations.Any(o => o.Slug == slug));

        public void Add(Organization organization) => Organizations.Add(organization);

        public Task<User?> FindByIdentityAsync(string issuer, string subject, CancellationToken cancellationToken) =>
            Task.FromResult(Users.SingleOrDefault(u => u.Issuer == issuer && u.Subject == subject));

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Email == email));

        public Task<User> ProvisionAsync(User candidate, CancellationToken cancellationToken)
        {
            Users.Add(candidate);
            return Task.FromResult(candidate);
        }

        public void Record(Guid organizationId, string entityType, Guid entityId, string action, object diff) =>
            AuditActions.Add(action);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnSave is not null)
            {
                throw ThrowOnSave;
            }

            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
