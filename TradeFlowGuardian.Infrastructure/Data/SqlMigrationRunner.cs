using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TradeFlowGuardian.Infrastructure.Data;

/// <summary>
/// A numbered SQL migration. Checksum is SHA-256 of the raw file content —
/// used to detect edits to a migration after it has been applied (forbidden).
/// </summary>
public sealed record MigrationScript(int Version, string Name, string Sql)
{
    public string Checksum { get; } =
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Sql))).ToLowerInvariant();
}

/// <summary>
/// Applies numbered SQL migrations (001_name.sql, 002_name.sql, ...) embedded in an
/// assembly, tracking applied versions in a schema_versions table. Designed to run as
/// a Railway pre-deploy command (<c>--migrate-only</c>): deterministic, idempotent, and
/// guarded by a Postgres advisory lock so two instances cannot double-apply.
/// </summary>
public sealed class SqlMigrationRunner(string connectionString, ILogger logger)
{
    // Session-level advisory lock key shared by every runner instance ("TFGMIGR").
    private const long AdvisoryLockKey = 0x54_46_47_4D_49_47_52;

    private const string EnsureTableSql =
        """
        CREATE TABLE IF NOT EXISTS schema_versions (
            version    INT          PRIMARY KEY,
            name       TEXT         NOT NULL,
            applied_at TIMESTAMPTZ  NOT NULL DEFAULT now(),
            checksum   TEXT         NOT NULL
        );
        """;

    private static readonly Regex ResourceNamePattern =
        new(@"(?<version>\d{3})_(?<name>[^.]+)\.sql$", RegexOptions.Compiled);

    /// <summary>
    /// Discovers embedded *.sql migration resources (e.g. "...Migrations.001_trade_history.sql")
    /// and returns them ordered by version. Throws on duplicate version numbers.
    /// </summary>
    public static IReadOnlyList<MigrationScript> LoadEmbedded(Assembly assembly)
    {
        var scripts = new List<MigrationScript>();
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!TryParseResourceName(resourceName, out var version, out var name))
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Cannot open embedded resource '{resourceName}'.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            scripts.Add(new MigrationScript(version, name, reader.ReadToEnd()));
        }

        var duplicates = scripts.GroupBy(s => s.Version).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                $"Duplicate migration version number(s): {string.Join(", ", duplicates)}.");

        return scripts.OrderBy(s => s.Version).ToList();
    }

    internal static bool TryParseResourceName(string resourceName, out int version, out string name)
    {
        var match = ResourceNamePattern.Match(resourceName);
        version = match.Success ? int.Parse(match.Groups["version"].Value) : 0;
        name    = match.Success ? match.Groups["name"].Value : string.Empty;
        return match.Success;
    }

    /// <summary>
    /// Applies all pending migrations in version order, each in its own transaction.
    /// Returns 0 on success (including no-op), 1 on any failure — checksum mismatch on an
    /// already-applied migration, a missing file for an applied version, or SQL error.
    /// </summary>
    public async Task<int> RunAsync(IReadOnlyList<MigrationScript> scripts, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await conn.ExecuteAsync("SELECT pg_advisory_lock(@key)", new { key = AdvisoryLockKey });
            try
            {
                await conn.ExecuteAsync(EnsureTableSql);

                var applied = (await conn.QueryAsync<(int Version, string Checksum)>(
                    "SELECT version, checksum FROM schema_versions ORDER BY version")).ToList();

                foreach (var (version, checksum) in applied)
                {
                    var script = scripts.FirstOrDefault(s => s.Version == version);
                    if (script is null)
                    {
                        logger.LogError(
                            "Migration {Version} is recorded as applied but its file is missing from the build. " +
                            "Applied migrations are immutable and must remain in the repo.", version);
                        return 1;
                    }
                    if (!string.Equals(script.Checksum, checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogError(
                            "Checksum mismatch for already-applied migration {Version} ({Name}): " +
                            "expected {Expected}, file has {Actual}. Migration files are immutable once applied — " +
                            "revert the edit and add a new migration instead.",
                            version, script.Name, checksum, script.Checksum);
                        return 1;
                    }
                }

                var appliedVersions = applied.Select(a => a.Version).ToHashSet();
                var pending = scripts.Where(s => !appliedVersions.Contains(s.Version)).ToList();
                if (pending.Count == 0)
                {
                    logger.LogInformation(
                        "No pending migrations — {Count} already applied, schema is current.", applied.Count);
                    return 0;
                }

                foreach (var script in pending)
                {
                    ct.ThrowIfCancellationRequested();
                    logger.LogInformation("Applying migration {Version} ({Name})...", script.Version, script.Name);
                    try
                    {
                        await using var tx = await conn.BeginTransactionAsync(ct);
                        await conn.ExecuteAsync(script.Sql, transaction: tx);
                        await conn.ExecuteAsync(
                            "INSERT INTO schema_versions (version, name, checksum) VALUES (@Version, @Name, @Checksum)",
                            new { script.Version, script.Name, script.Checksum }, tx);
                        await tx.CommitAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Migration {Version} ({Name}) failed — rolled back, stopping. " +
                            "No later migrations were attempted.", script.Version, script.Name);
                        return 1;
                    }
                    logger.LogInformation("Migration {Version} ({Name}) applied.", script.Version, script.Name);
                }

                logger.LogInformation("Applied {Count} migration(s); schema is current.", pending.Count);
                return 0;
            }
            finally
            {
                await conn.ExecuteAsync("SELECT pg_advisory_unlock(@key)", new { key = AdvisoryLockKey });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration run failed before any migration could be applied.");
            return 1;
        }
    }

    /// <summary>
    /// Records versions 1..<paramref name="upToVersion"/> in schema_versions WITHOUT executing
    /// them — for adopting a database whose schema was applied manually. Refuses to run if
    /// schema_versions already has rows.
    /// </summary>
    public async Task<int> BaselineAsync(
        IReadOnlyList<MigrationScript> scripts, int upToVersion, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await conn.ExecuteAsync("SELECT pg_advisory_lock(@key)", new { key = AdvisoryLockKey });
            try
            {
                await conn.ExecuteAsync(EnsureTableSql);

                var existing = await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM schema_versions");
                if (existing > 0)
                {
                    logger.LogError(
                        "Refusing to baseline: schema_versions already has {Count} row(s). " +
                        "Baseline is only for adopting a database that has never been tracked.", existing);
                    return 1;
                }

                var toRecord = scripts.Where(s => s.Version <= upToVersion).OrderBy(s => s.Version).ToList();
                var missing = Enumerable.Range(1, upToVersion).Except(toRecord.Select(s => s.Version)).ToList();
                if (missing.Count > 0)
                {
                    logger.LogError(
                        "Refusing to baseline to version {UpTo}: migration file(s) {Missing} not found in the build.",
                        upToVersion, string.Join(", ", missing));
                    return 1;
                }

                await using var tx = await conn.BeginTransactionAsync(ct);
                foreach (var script in toRecord)
                    await conn.ExecuteAsync(
                        "INSERT INTO schema_versions (version, name, checksum) VALUES (@Version, @Name, @Checksum)",
                        new { script.Version, script.Name, script.Checksum }, tx);
                await tx.CommitAsync(ct);

                logger.LogInformation(
                    "Baselined schema_versions at version {UpTo} ({Count} row(s) recorded, nothing executed).",
                    upToVersion, toRecord.Count);
                return 0;
            }
            finally
            {
                await conn.ExecuteAsync("SELECT pg_advisory_unlock(@key)", new { key = AdvisoryLockKey });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Baseline failed.");
            return 1;
        }
    }

    /// <summary>
    /// Returns migrations not yet recorded in schema_versions (all of them if the table
    /// does not exist). Read-only — takes no lock. Used for the startup pending-migrations warning.
    /// </summary>
    public async Task<IReadOnlyList<MigrationScript>> GetPendingAsync(
        IReadOnlyList<MigrationScript> scripts, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var tableExists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_versions')");
        if (!tableExists)
            return scripts;

        var applied = (await conn.QueryAsync<int>("SELECT version FROM schema_versions")).ToHashSet();
        return scripts.Where(s => !applied.Contains(s.Version)).ToList();
    }
}
