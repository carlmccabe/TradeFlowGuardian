using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TradeFlowGuardian.Core.Interfaces;

namespace TradeFlowGuardian.Infrastructure.Cache;

/// <summary>
/// Redis-backed position state cache. TTL is 5 minutes — long enough to avoid redundant OANDA
/// round-trips within a normal signal burst, short enough to self-heal after an external close
/// (SL hit, manual close) without operator intervention.
/// </summary>
public sealed class RedisPositionCache(IConnectionMultiplexer redis, ILogger<RedisPositionCache> logger)
    : IPositionCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private readonly IDatabase _db = redis.GetDatabase();

    private static string Key(string instrument) => $"tradeflow:position:{instrument}";

    public async Task<(bool Found, decimal? Units)> GetAsync(string instrument, CancellationToken ct = default)
    {
        try
        {
            var value = await _db.StringGetAsync(Key(instrument));
            if (value.IsNull)
                return (false, null);

            if (decimal.TryParse((string?)value, out var units))
                return (true, units);

            logger.LogWarning("Unexpected value in position cache for {Instrument}: '{Value}' — treating as miss", instrument, value);
            return (false, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis error reading position cache for {Instrument}", instrument);
            return (false, null);
        }
    }

    public async Task SetAsync(string instrument, decimal units, CancellationToken ct = default)
    {
        try
        {
            await _db.StringSetAsync(Key(instrument), units.ToString(), Ttl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis error writing position cache for {Instrument} ({Units} units)", instrument, units);
        }
    }

    public async Task ClearAsync(string instrument, CancellationToken ct = default)
    {
        try
        {
            await _db.KeyDeleteAsync(Key(instrument));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis error clearing position cache for {Instrument}", instrument);
        }
    }
}
