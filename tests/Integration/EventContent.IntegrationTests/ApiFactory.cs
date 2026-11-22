using EventContent.Migrator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace EventContent.IntegrationTests;

/// <summary>Boots the real API against a throwaway PostgreSQL migrated with the production migrator.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await new SqlMigrator(_postgres.GetConnectionString(), MigrationSource.FromEmbeddedResources(typeof(SqlMigrator).Assembly), TextWriter.Null)
            .MigrateAsync(CancellationToken.None);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient ClientFor(string email)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", email);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
        builder.UseSetting("Auth:Mode", "Development");
        builder.UseSetting("Auth:Development:DefaultUserEmail", "default@example.com");
        builder.UseSetting("RateLimiting:RequestsPerMinute", "10000");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
