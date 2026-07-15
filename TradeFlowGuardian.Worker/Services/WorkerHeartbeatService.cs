using System.Text.Json;
using StackExchange.Redis;

namespace TradeFlowGuardian.Worker.Services;

/// <summary>
/// Publishes a heartbeat to Redis every 15s so the Api's readiness endpoint can prove
/// the Worker is alive and report which build it is running. Key expires after 60s,
/// so a dead or wedged Worker surfaces as a missing/stale heartbeat within a minute.
/// </summary>
public class WorkerHeartbeatService(
    IConnectionMultiplexer redis,
    ILogger<WorkerHeartbeatService> logger) : BackgroundService
{
    public const string Key = "tradeflow:worker:heartbeat";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly string _sha = BuildInfo.GitSha;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = redis.GetDatabase();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    sha       = _sha,
                    startedAt = _startedAt,
                    beatAt    = DateTimeOffset.UtcNow
                });
                await db.StringSetAsync(Key, payload, Ttl);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to write worker heartbeat");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

/// <summary>
/// Resolves the running build's git SHA. Railway injects RAILWAY_GIT_COMMIT_SHA;
/// GIT_SHA is the docker-compose/local override; "unknown" means neither is set.
/// </summary>
public static class BuildInfo
{
    public static string GitSha =>
        Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA")
        ?? Environment.GetEnvironmentVariable("GIT_SHA")
        ?? "unknown";
}
