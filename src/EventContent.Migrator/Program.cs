using EventContent.Migrator;

// Applies the SQL migrations embedded from /db/migrations.
// Usage: dotnet run --project src/EventContent.Migrator  (connection string from ConnectionStrings__Database)
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
if (string.IsNullOrWhiteSpace(connectionString))
{
    await Console.Error.WriteLineAsync("Set ConnectionStrings__Database to the target PostgreSQL connection string.");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

try
{
    var applied = await new SqlMigrator(connectionString, MigrationSource.FromEmbeddedResources(), Console.Out)
        .MigrateAsync(cancellation.Token);
    await Console.Out.WriteLineAsync($"Database is up to date ({applied} migration(s) applied).");
    return 0;
}
catch (MigrationException ex)
{
    await Console.Error.WriteLineAsync($"Migration failed: {ex.Message}");
    return 1;
}
