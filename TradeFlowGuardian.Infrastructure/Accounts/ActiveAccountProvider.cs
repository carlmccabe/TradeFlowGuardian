using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Accounts;

/// <summary>
/// Singleton resolver for the active OANDA account. Reads the registry through a
/// short cache, and subscribes to the Redis account-changed channel so a switch
/// made in the Api takes effect in the Worker within milliseconds. Falls back to
/// the legacy Oanda config section when the registry has no active account
/// (first deploy before seeding, or Postgres not configured).
/// </summary>
public class ActiveAccountProvider : IActiveAccountProvider, IDisposable
{
    public const string ChangedChannel = "tradeflow:account-changed";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly OandaConfig? _fallbackConfig;
    private readonly ILogger<ActiveAccountProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ActiveOandaAccount? _cached;
    private DateTime _cachedAtUtc = DateTime.MinValue;

    public ActiveAccountProvider(
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer redis,
        IOptions<OandaConfig> fallback,
        ILogger<ActiveAccountProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _logger = logger;

        // Config with placeholder values (default appsettings.json) is no fallback at all.
        var cfg = fallback.Value;
        _fallbackConfig = IsUsable(cfg) ? cfg : null;

        _redis.GetSubscriber().Subscribe(
            RedisChannel.Literal(ChangedChannel),
            (_, _) =>
            {
                _logger.LogInformation("Account-changed event received — invalidating cached account");
                Invalidate();
            });
    }

    public async Task<ActiveOandaAccount> GetActiveAsync(CancellationToken ct = default)
    {
        var cached = _cached;
        if (cached is not null && DateTime.UtcNow - _cachedAtUtc < CacheTtl)
            return cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cached is not null && DateTime.UtcNow - _cachedAtUtc < CacheTtl)
                return _cached;

            var account = await ReadFromStoreAsync(ct);

            if (account is null && _fallbackConfig is not null)
            {
                _logger.LogWarning(
                    "No active account in registry — falling back to Oanda config section ({Env} {AccountId})",
                    _fallbackConfig.Environment, _fallbackConfig.AccountId);
                account = new ActiveOandaAccount(
                    _fallbackConfig.AccountId, _fallbackConfig.ApiKey,
                    _fallbackConfig.Environment, "config-fallback");
            }

            _cached = account ?? throw new InvalidOperationException(
                "No active OANDA account — register one via POST /api/accounts or configure the Oanda section.");
            _cachedAtUtc = DateTime.UtcNow;
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate()
    {
        _cached = null;
        _cachedAtUtc = DateTime.MinValue;
    }

    private async Task<ActiveOandaAccount?> ReadFromStoreAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            // Optional: not registered when Postgres is not configured.
            var store = scope.ServiceProvider.GetService<IOandaAccountStore>();
            if (store is null) return null;
            return await store.GetActiveAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read active account from registry");
            return null;
        }
    }

    internal static bool IsUsable(OandaConfig cfg) =>
        !string.IsNullOrWhiteSpace(cfg.ApiKey) &&
        !string.IsNullOrWhiteSpace(cfg.AccountId) &&
        !cfg.ApiKey.StartsWith("REPLACE_WITH", StringComparison.OrdinalIgnoreCase) &&
        !cfg.AccountId.StartsWith("REPLACE_WITH", StringComparison.OrdinalIgnoreCase);

    public void Dispose() => _gate.Dispose();
}
