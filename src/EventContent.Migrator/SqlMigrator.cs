using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace EventContent.Migrator;

public sealed record Migration(int Version, string Name, string Sql)
{
    public string Checksum { get; } = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Sql.ReplaceLineEndings("\n"))));
}

public sealed class MigrationException(string message, Exception? inner = null) : Exception(message, inner);

public static partial class MigrationSource
{
    /// <summary>Loads <c>NNNN_name.sql</c> files embedded under <c>migrations/</c>, ordered by version.</summary>
    public static IReadOnlyList<Migration> FromEmbeddedResources(Assembly? assembly = null)
    {
        assembly ??= typeof(MigrationSource).Assembly;
        var migrations = new List<Migration>();

        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith("migrations/", StringComparison.Ordinal)))
        {
            var fileName = resource["migrations/".Length..];
            var match = FileNamePattern().Match(fileName);
            if (!match.Success)
            {
                throw new MigrationException($"Migration file '{fileName}' must be named NNNN_description.sql.");
            }

            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new MigrationException($"Cannot read embedded migration '{resource}'.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            migrations.Add(new Migration(
                int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture),
                match.Groups["name"].Value,
                reader.ReadToEnd()));
        }

        var duplicates = migrations.GroupBy(m => m.Version).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            throw new MigrationException($"Duplicate migration versions: {string.Join(", ", duplicates)}.");
        }

        return [.. migrations.OrderBy(m => m.Version)];
    }

    [GeneratedRegex(@"^(?<version>\d{4})_(?<name>[a-z0-9_]+)\.sql$", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();
}

/// <summary>
/// Forward-only SQL migrator. Each migration runs in its own transaction; a PostgreSQL advisory lock
/// prevents concurrent runners; checksums detect edits to already-applied migrations.
/// </summary>
public sealed class SqlMigrator(string connectionString, IReadOnlyList<Migration> migrations, TextWriter log)
{
    // Arbitrary constant identifying this application's migration lock.
    private const long AdvisoryLockKey = 0x45_43_53_4D_49_47; // "ECSMIG"

    public async Task<int> MigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, null, $"SELECT pg_advisory_lock({AdvisoryLockKey})", cancellationToken);
        try
        {
            await ExecuteAsync(connection, null, """
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    version     integer     PRIMARY KEY,
                    name        text        NOT NULL,
                    checksum    text        NOT NULL,
                    applied_at  timestamptz NOT NULL DEFAULT now()
                )
                """, cancellationToken);

            var applied = await LoadAppliedAsync(connection, cancellationToken);
            VerifyChecksums(applied);

            var count = 0;
            foreach (var migration in migrations.Where(m => !applied.ContainsKey(m.Version)))
            {
                await log.WriteLineAsync($"Applying {migration.Version:D4}_{migration.Name}...");
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    await ExecuteAsync(connection, transaction, migration.Sql, cancellationToken);
                    await using var record = new NpgsqlCommand(
                        "INSERT INTO schema_migrations (version, name, checksum) VALUES (@version, @name, @checksum)",
                        connection,
                        transaction);
                    record.Parameters.AddWithValue("version", migration.Version);
                    record.Parameters.AddWithValue("name", migration.Name);
                    record.Parameters.AddWithValue("checksum", migration.Checksum);
                    await record.ExecuteNonQueryAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    count++;
                }
                catch (PostgresException ex)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw new MigrationException($"{migration.Version:D4}_{migration.Name}: {ex.MessageText}", ex);
                }
            }

            return count;
        }
        finally
        {
            await ExecuteAsync(connection, null, $"SELECT pg_advisory_unlock({AdvisoryLockKey})", CancellationToken.None);
        }
    }

    private void VerifyChecksums(Dictionary<int, string> applied)
    {
        foreach (var migration in migrations)
        {
            if (applied.TryGetValue(migration.Version, out var checksum) && checksum != migration.Checksum)
            {
                throw new MigrationException(
                    $"Migration {migration.Version:D4}_{migration.Name} was modified after it was applied. " +
                    "Never edit applied migrations; add a new one instead.");
            }
        }

        var unknown = applied.Keys.Except(migrations.Select(m => m.Version)).ToList();
        if (unknown.Count > 0)
        {
            throw new MigrationException(
                $"The database has migrations this build does not know about: {string.Join(", ", unknown)}. Deploy a newer build.");
        }
    }

    private static async Task<Dictionary<int, string>> LoadAppliedAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var applied = new Dictionary<int, string>();
        await using var command = new NpgsqlCommand("SELECT version, checksum FROM schema_migrations", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied[reader.GetInt32(0)] = reader.GetString(1);
        }

        return applied;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
