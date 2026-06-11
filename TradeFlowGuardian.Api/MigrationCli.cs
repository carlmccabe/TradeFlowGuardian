using System.Reflection;
using TradeFlowGuardian.Infrastructure.Data;

namespace TradeFlowGuardian.Api;

/// <summary>
/// Host-less entry point for the Railway pre-deploy command. Handles
/// <c>--migrate-only</c> (apply pending migrations) and <c>--migrate-baseline N</c>
/// (record 001..N as applied without executing — adoption only). Never starts
/// Kestrel or any hosted services; normal app startup never migrates.
/// </summary>
internal static class MigrationCli
{
    private const string MigrateOnlyArg = "--migrate-only";
    private const string BaselineArg = "--migrate-baseline";

    public static bool IsMigrationCommand(string[] args) =>
        args.Contains(MigrateOnlyArg) || args.Contains(BaselineArg);

    public static async Task<int> RunAsync(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        // Mirror normal startup configuration sources, minus the command line
        // (our args are not config) and without building a host.
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true);
        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
            configBuilder.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
        var config = configBuilder.AddEnvironmentVariables().Build();

        // Same logging shape as the app: JSON single-line on Railway, plain console in dev.
        using var loggerFactory = LoggerFactory.Create(logging =>
        {
            if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
                logging.AddConsole();
            else
                logging.AddJsonConsole(opts =>
                {
                    opts.IncludeScopes = true;
                    opts.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
                    opts.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
                });
        });
        var logger = loggerFactory.CreateLogger("Migrations");

        var connectionString = PostgresConnectionHelper.Normalize(config["Postgres:ConnectionString"]);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogError("Postgres:ConnectionString is missing or invalid — cannot run migrations.");
            return 1;
        }

        var scripts = SqlMigrationRunner.LoadEmbedded(typeof(MigrationCli).Assembly);
        var runner = new SqlMigrationRunner(connectionString, logger);

        if (args.Contains(BaselineArg))
        {
            var index = Array.IndexOf(args, BaselineArg);
            if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out var upToVersion) || upToVersion < 1)
            {
                logger.LogError("{Arg} requires an explicit version, e.g. '{Arg} 4'.", BaselineArg, BaselineArg);
                return 1;
            }
            logger.LogInformation("Baselining schema_versions at version {Version}...", upToVersion);
            return await runner.BaselineAsync(scripts, upToVersion);
        }

        logger.LogInformation("Running migrations ({Count} file(s) in build)...", scripts.Count);
        return await runner.RunAsync(scripts);
    }

    /// <summary>
    /// Best-effort startup check that logs a warning if pending migrations are detected.
    /// Log-and-swallow: a DB outage or missing config must never affect startup.
    /// </summary>
    public static async Task WarnIfPendingAsync(string connectionString, ILogger logger)
    {
        try
        {
            var scripts = SqlMigrationRunner.LoadEmbedded(typeof(MigrationCli).Assembly);
            var runner = new SqlMigrationRunner(connectionString, logger);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var pending = await runner.GetPendingAsync(scripts, cts.Token);
            if (pending.Count > 0)
                logger.LogWarning(
                    "{Count} pending migration(s) detected ({Versions}) — the pre-deploy command applies these; " +
                    "the app never migrates at startup.",
                    pending.Count, string.Join(", ", pending.Select(p => $"{p.Version:000}_{p.Name}")));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pending-migration check failed (non-fatal).");
        }
    }
}
