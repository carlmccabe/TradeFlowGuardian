using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TradeFlowGuardian.Infrastructure.Data;

namespace TradeFlowGuardian.Tests;

// ── Unit tests (no database) ──────────────────────────────────────────────────

public class MigrationScriptTests
{
    [Fact]
    public void Checksum_IsDeterministic_AndChangesWithContent()
    {
        var a1 = new MigrationScript(1, "test", "CREATE TABLE t (id int);");
        var a2 = new MigrationScript(1, "test", "CREATE TABLE t (id int);");
        var b  = new MigrationScript(1, "test", "CREATE TABLE t (id bigint);");

        Assert.Equal(a1.Checksum, a2.Checksum);
        Assert.NotEqual(a1.Checksum, b.Checksum);
        Assert.Equal(64, a1.Checksum.Length); // SHA-256 hex
    }

    [Theory]
    [InlineData("TradeFlowGuardian.Api.Migrations.001_trade_history.sql", true, 1, "trade_history")]
    [InlineData("Some.Other.Prefix.042_add_widgets.sql", true, 42, "add_widgets")]
    [InlineData("TradeFlowGuardian.Api.Migrations.readme.txt", false, 0, "")]
    [InlineData("TradeFlowGuardian.Api.Migrations.1_no_padding.sql", false, 0, "")]
    public void TryParseResourceName_ParsesVersionedSqlOnly(
        string resourceName, bool expectMatch, int expectedVersion, string expectedName)
    {
        var matched = SqlMigrationRunner.TryParseResourceName(resourceName, out var version, out var name);

        Assert.Equal(expectMatch, matched);
        if (expectMatch)
        {
            Assert.Equal(expectedVersion, version);
            Assert.Equal(expectedName, name);
        }
    }

    [Fact]
    public void LoadEmbedded_FindsAllRepoMigrations_Ordered()
    {
        var scripts = SqlMigrationRunner.LoadEmbedded(typeof(Api.MigrationCli).Assembly);

        Assert.True(scripts.Count >= 4, "expected at least migrations 001-004 embedded in the Api assembly");
        Assert.Equal(scripts.OrderBy(s => s.Version).Select(s => s.Version), scripts.Select(s => s.Version));
        Assert.Equal([1, 2, 3, 4], scripts.Take(4).Select(s => s.Version));
        Assert.Equal("trade_history", scripts[0].Name);
        Assert.All(scripts, s => Assert.False(string.IsNullOrWhiteSpace(s.Sql)));
    }
}

// ── Integration tests (real Postgres, gated behind TFG_TEST_POSTGRES) ────────
// Set TFG_TEST_POSTGRES to a connection string whose user can CREATE DATABASE,
// e.g. "Host=localhost;Username=postgres;Password=postgres". Each test runs in
// a scratch database that is dropped afterwards.

public sealed class PostgresFactAttribute : FactAttribute
{
    public const string EnvVar = "TFG_TEST_POSTGRES";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar)))
            Skip = $"Set {EnvVar} to a Postgres connection string (CREATEDB rights) to run migration integration tests.";
    }
}

public sealed class MigrationRunnerPostgresTests : IAsyncLifetime
{
    private readonly string _dbName = $"tfg_mig_test_{Guid.NewGuid():N}";
    private string _adminCs = string.Empty;
    private string _testCs = string.Empty;

    public async Task InitializeAsync()
    {
        _adminCs = Environment.GetEnvironmentVariable(PostgresFactAttribute.EnvVar)!;
        await using var conn = new NpgsqlConnection(_adminCs);
        await conn.OpenAsync();
        await conn.ExecuteAsync($"CREATE DATABASE \"{_dbName}\"");
        _testCs = new NpgsqlConnectionStringBuilder(_adminCs) { Database = _dbName }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(_adminCs);
        await conn.OpenAsync();
        await conn.ExecuteAsync($"DROP DATABASE \"{_dbName}\" WITH (FORCE)");
    }

    private SqlMigrationRunner CreateRunner() => new(_testCs, NullLogger.Instance);

    private static MigrationScript Script(int version, string name, string sql) => new(version, name, sql);

    private async Task<T> QueryScalarAsync<T>(string sql)
    {
        await using var conn = new NpgsqlConnection(_testCs);
        return await conn.ExecuteScalarAsync<T>(sql) ?? throw new InvalidOperationException("null scalar");
    }

    [PostgresFact]
    public async Task Run_AppliesInOrder_AndSecondRunIsNoOp()
    {
        var scripts = new[]
        {
            Script(1, "one", "CREATE TABLE mig_t1 (id int PRIMARY KEY);"),
            Script(2, "two", "INSERT INTO mig_t1 VALUES (42);"), // depends on 001 — proves ordering
        };

        Assert.Equal(0, await CreateRunner().RunAsync(scripts));
        Assert.Equal(42, await QueryScalarAsync<int>("SELECT id FROM mig_t1"));
        Assert.Equal(2, await QueryScalarAsync<int>("SELECT count(*) FROM schema_versions"));

        // Idempotent: second run applies nothing.
        Assert.Equal(0, await CreateRunner().RunAsync(scripts));
        Assert.Equal(1, await QueryScalarAsync<int>("SELECT count(*) FROM mig_t1"));
        Assert.Equal(2, await QueryScalarAsync<int>("SELECT count(*) FROM schema_versions"));
    }

    [PostgresFact]
    public async Task Run_HardFailsWhenAppliedMigrationWasEdited()
    {
        var original = new[] { Script(1, "one", "CREATE TABLE mig_t1 (id int);") };
        Assert.Equal(0, await CreateRunner().RunAsync(original));

        var tampered = new[] { Script(1, "one", "CREATE TABLE mig_t1 (id bigint);") };
        Assert.Equal(1, await CreateRunner().RunAsync(tampered));
    }

    [PostgresFact]
    public async Task Run_HardFailsWhenAppliedMigrationFileIsMissing()
    {
        Assert.Equal(0, await CreateRunner().RunAsync([Script(1, "one", "CREATE TABLE mig_t1 (id int);")]));
        Assert.Equal(1, await CreateRunner().RunAsync([]));
    }

    [PostgresFact]
    public async Task Run_StopsAtFirstFailure_LaterMigrationsNotAttempted()
    {
        var scripts = new[]
        {
            Script(1, "good", "CREATE TABLE mig_t1 (id int);"),
            Script(2, "bad", "THIS IS NOT SQL;"),
            Script(3, "never", "CREATE TABLE mig_t3 (id int);"),
        };

        Assert.Equal(1, await CreateRunner().RunAsync(scripts));
        Assert.Equal(1, await QueryScalarAsync<int>("SELECT count(*) FROM schema_versions")); // only 001
        Assert.False(await QueryScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'mig_t3')"));
    }

    [PostgresFact]
    public async Task Baseline_RecordsWithoutExecuting_ThenRunAppliesOnlyNewer()
    {
        var scripts = new[]
        {
            Script(1, "one", "CREATE TABLE mig_t1 (id int);"),
            Script(2, "two", "CREATE TABLE mig_t2 (id int);"),
            Script(3, "three", "CREATE TABLE mig_t3 (id int);"),
        };

        Assert.Equal(0, await CreateRunner().BaselineAsync(scripts, upToVersion: 2));

        // Recorded but not executed.
        Assert.Equal(2, await QueryScalarAsync<int>("SELECT count(*) FROM schema_versions"));
        Assert.False(await QueryScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'mig_t1')"));

        // A subsequent run applies only 003.
        Assert.Equal(0, await CreateRunner().RunAsync(scripts));
        Assert.False(await QueryScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'mig_t1')"));
        Assert.True(await QueryScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'mig_t3')"));
    }

    [PostgresFact]
    public async Task Baseline_RefusesWhenSchemaVersionsHasRows()
    {
        var scripts = new[] { Script(1, "one", "CREATE TABLE mig_t1 (id int);") };
        Assert.Equal(0, await CreateRunner().RunAsync(scripts));

        Assert.Equal(1, await CreateRunner().BaselineAsync(scripts, upToVersion: 1));
    }

    [PostgresFact]
    public async Task Baseline_RefusesWhenAFileInRangeIsMissing()
    {
        var scripts = new[] { Script(1, "one", "CREATE TABLE mig_t1 (id int);") }; // no 002
        Assert.Equal(1, await CreateRunner().BaselineAsync(scripts, upToVersion: 2));
        Assert.Equal(0, await QueryScalarAsync<int>("SELECT count(*) FROM schema_versions"));
    }

    [PostgresFact]
    public async Task AdvisoryLock_PreventsConcurrentDoubleApply()
    {
        // pg_sleep widens the race window; without the advisory lock both runners
        // would execute the INSERT before either records the version row.
        var scripts = new[]
        {
            Script(1, "probe",
                """
                CREATE TABLE IF NOT EXISTS mig_probe (id serial PRIMARY KEY);
                SELECT pg_sleep(1);
                INSERT INTO mig_probe DEFAULT VALUES;
                """),
        };

        var results = await Task.WhenAll(
            CreateRunner().RunAsync(scripts),
            CreateRunner().RunAsync(scripts));

        Assert.All(results, r => Assert.Equal(0, r));
        Assert.Equal(1, await QueryScalarAsync<int>("SELECT count(*) FROM mig_probe"));
        Assert.Equal(1, await QueryScalarAsync<int>("SELECT count(*) FROM schema_versions"));
    }

    [PostgresFact]
    public async Task GetPending_ReportsAllWhenUntracked_ThenNoneAfterRun()
    {
        var scripts = new[] { Script(1, "one", "CREATE TABLE mig_t1 (id int);") };
        var runner = CreateRunner();

        Assert.Single(await runner.GetPendingAsync(scripts));
        Assert.Equal(0, await runner.RunAsync(scripts));
        Assert.Empty(await runner.GetPendingAsync(scripts));
    }
}
