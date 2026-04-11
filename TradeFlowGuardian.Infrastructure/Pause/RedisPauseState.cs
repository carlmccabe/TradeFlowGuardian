using StackExchange.Redis;
using TradeFlowGuardian.Core.Interfaces;

namespace TradeFlowGuardian.Infrastructure.Pause;

/// <summary>
/// Redis-backed global pause flag.
/// Key: tradeflow:paused → "1" when paused, absent when running.
/// No TTL — persists until explicitly cleared via SetPausedAsync(false).
/// </summary>
public class RedisPauseState(IConnectionMultiplexer redis) : IPauseState
{
    private readonly IDatabase _db = redis.GetDatabase();
    private const string Key = "tradeflow:paused";

    public async Task<bool> IsPausedAsync(CancellationToken ct = default)
        => (await _db.StringGetAsync(Key)).HasValue;

    public async Task SetPausedAsync(bool paused, CancellationToken ct = default)
    {
        if (paused)
            await _db.StringSetAsync(Key, "1");
        else
            await _db.KeyDeleteAsync(Key);
    }
}
