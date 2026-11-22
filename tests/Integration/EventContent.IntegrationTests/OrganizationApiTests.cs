using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventContent.Contracts.Audit;
using EventContent.Contracts.Common;
using EventContent.Contracts.Organizations;
using EventContent.Contracts.Users;
using EventContent.Infrastructure.Persistence;
using EventContent.Migrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventContent.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class OrganizationApiTests(ApiFactory factory)
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private static async Task<OrganizationResponse> CreateOrgAsync(HttpClient client, string? slug = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/organizations",
            new CreateOrganizationRequest("Test Org", slug ?? Unique("org"), "Africa/Lagos"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrganizationResponse>())!;
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    [Fact]
    public async Task Me_provisions_the_user_on_first_request()
    {
        var email = $"{Unique("me")}@example.com";
        var me = await factory.ClientFor(email).GetFromJsonAsync<CurrentUserResponse>("/api/v1/me");

        Assert.NotNull(me);
        Assert.Equal(email, me.Email);
        Assert.Empty(me.Organizations);
    }

    [Fact]
    public async Task Creator_becomes_owner_and_sees_the_organization()
    {
        var client = factory.ClientFor($"{Unique("owner")}@example.com");
        var org = await CreateOrgAsync(client);

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/me");
        var summary = Assert.Single(me!.Organizations);
        Assert.Equal(org.Id, summary.Id);
        Assert.Equal("Owner", summary.MyRole);
    }

    [Fact]
    public async Task Duplicate_slug_is_a_conflict_with_a_stable_code()
    {
        var client = factory.ClientFor($"{Unique("dup")}@example.com");
        var slug = Unique("dup");
        await CreateOrgAsync(client, slug);

        var response = await client.PostAsJsonAsync("/api/v1/organizations", new CreateOrganizationRequest("Other", slug, "UTC"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("organization.slug_taken", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Invalid_input_returns_422_with_field_errors()
    {
        var client = factory.ClientFor($"{Unique("bad")}@example.com");

        var response = await client.PostAsJsonAsync("/api/v1/organizations", new CreateOrganizationRequest("x", "Bad Slug", "Nowhere/Land"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"slug\"", body, StringComparison.Ordinal);
        Assert.Contains("\"defaultTimeZone\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_members_cannot_see_another_tenant()
    {
        var owner = factory.ClientFor($"{Unique("a")}@example.com");
        var outsider = factory.ClientFor($"{Unique("b")}@example.com");
        var org = await CreateOrgAsync(owner);

        foreach (var path in new[] { "", "/members", "/audit-log" })
        {
            var response = await outsider.GetAsync($"/api/v1/organizations/{org.Id}{path}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        var add = await outsider.PostAsJsonAsync($"/api/v1/organizations/{org.Id}/members", new AddMemberRequest("x@example.com", "Owner"));
        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
    }

    [Fact]
    public async Task Viewer_can_read_but_not_manage_members()
    {
        var ownerEmail = $"{Unique("own")}@example.com";
        var viewerEmail = $"{Unique("view")}@example.com";
        var owner = factory.ClientFor(ownerEmail);
        var viewer = factory.ClientFor(viewerEmail);
        await viewer.GetAsync("/api/v1/me"); // provision
        var org = await CreateOrgAsync(owner);

        var added = await owner.PostAsJsonAsync($"/api/v1/organizations/{org.Id}/members", new AddMemberRequest(viewerEmail, "Viewer"));
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        var members = await viewer.GetFromJsonAsync<List<MemberResponse>>($"/api/v1/organizations/{org.Id}/members");
        Assert.Equal(2, members!.Count);

        var forbidden = await viewer.PostAsJsonAsync($"/api/v1/organizations/{org.Id}/members", new AddMemberRequest(ownerEmail, "Editor"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("auth.permission_denied", await ErrorCodeAsync(forbidden));
    }

    [Fact]
    public async Task The_last_owner_cannot_be_removed()
    {
        var owner = factory.ClientFor($"{Unique("last")}@example.com");
        var org = await CreateOrgAsync(owner);
        var members = await owner.GetFromJsonAsync<List<MemberResponse>>($"/api/v1/organizations/{org.Id}/members");

        var response = await owner.DeleteAsync($"/api/v1/organizations/{org.Id}/members/{members!.Single().Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("organization.last_owner", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Stale_version_update_is_rejected()
    {
        var owner = factory.ClientFor($"{Unique("ver")}@example.com");
        var org = await CreateOrgAsync(owner);

        var first = await owner.PatchAsJsonAsync($"/api/v1/organizations/{org.Id}", new UpdateOrganizationRequest("Renamed", "UTC", org.Version));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var stale = await owner.PatchAsJsonAsync($"/api/v1/organizations/{org.Id}", new UpdateOrganizationRequest("Again", "UTC", org.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("common.version_conflict", await ErrorCodeAsync(stale));
    }

    [Fact]
    public async Task Audit_log_records_changes_newest_first_with_pagination()
    {
        var ownerEmail = $"{Unique("aud")}@example.com";
        var owner = factory.ClientFor(ownerEmail);
        var org = await CreateOrgAsync(owner);
        await owner.PatchAsJsonAsync($"/api/v1/organizations/{org.Id}", new UpdateOrganizationRequest("Renamed", "UTC", org.Version));

        var page1 = await owner.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>($"/api/v1/organizations/{org.Id}/audit-log?pageSize=1");
        Assert.Equal("organization.updated", Assert.Single(page1!.Items).Action);
        Assert.NotNull(page1.NextCursor);

        var page2 = await owner.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            $"/api/v1/organizations/{org.Id}/audit-log?pageSize=1&cursor={page1.NextCursor}");
        Assert.Equal("organization.created", Assert.Single(page2!.Items).Action);
        Assert.Null(page2.NextCursor);

        var bad = await owner.GetAsync($"/api/v1/organizations/{org.Id}/audit-log?cursor=garbage");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
    }

    [Fact]
    public async Task Ef_model_matches_the_migrated_schema()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Each query selects every mapped column; a missing or misnamed column fails here.
        await db.Users.Take(1).ToListAsync();
        await db.Organizations.Take(1).ToListAsync();
        await db.OrganizationMembers.IgnoreQueryFilters().Take(1).ToListAsync();
        await db.AuditLogs.IgnoreQueryFilters().Take(1).ToListAsync();
    }

    [Fact]
    public async Task Migrations_are_idempotent()
    {
        using var scope = factory.Services.CreateScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString()!;

        var applied = await new SqlMigrator(connectionString, MigrationSource.FromEmbeddedResources(typeof(SqlMigrator).Assembly), TextWriter.Null)
            .MigrateAsync(CancellationToken.None);

        Assert.Equal(0, applied);
    }

    [Fact]
    public async Task Responses_carry_security_headers_and_health_is_public()
    {
        var response = await factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task Invalid_dev_user_header_is_rejected()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "not an email");

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
