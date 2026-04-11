using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;

namespace TradeFlowGuardian.Infrastructure.Drawdown;

/// <summary>
/// Redis-backed daily drawdown circuit breaker.
///
/// Redis key schema (date-scoped, 48h TTL):
///   drawdown:nav:{yyyyMMdd}      — day-open NAV (decimal string, set once via SetNX)
///   drawdown:breached:{yyyyMMdd} — present when today's drawdown limit is breached
///
/// The date in each key ensures automatic logical reset at UTC midnight with no
/// scheduled job required. Keys are cleaned up by Redis TTL within 48h.
/// </summary>
public class DailyDrawdownGuard(
    IConnectionMultiplexer redis,
    IOptions<RiskConfig> risk,
    ILogger<DailyDrawdownGuard> logger) : IDailyDrawdownGuard
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly decimal _maxDrawdownPct = risk.Value.MaxDailyDrawdownPercent;

    private static readonly TimeSpan KeyTtl = TimeSpan.FromHours(48);

    // Keys are re-evaluated on every call so they always reflect the current UTC date.
    private static string DayStamp => DateTime.UtcNow.ToString("yyyyMMdd");
    private static string NavKey => $"drawdown:nav:{DayStamp}";
    private static string BreachedKey => $"drawdown:breached:{DayStamp}";

    public async Task<bool> IsBreachedAsync(CancellationToken ct = default)
        => (await _db.StringGetAsync(BreachedKey)).HasValue;

    public async Task EnsureDayOpenNavAsync(decimal currentBalance, CancellationToken ct = default)
    {
        var set = await _db.StringSetAsync(
            NavKey,
            currentBalance.ToString(CultureInfo.InvariantCulture),
            KeyTtl,
            When.NotExists);

        if (set)
            logger.LogInformation(
                "Day-open NAV recorded for {Day}: {Nav:C}", DayStamp, currentBalance);
    }

    public async Task<bool> CheckAndMarkIfBreachedAsync(decimal currentBalance, CancellationToken ct = default)
    {
        var navStr = await _db.StringGetAsync(NavKey);
        if (!navStr.HasValue)
            return false;

        if (!decimal.TryParse((string?)navStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var dayOpenNav) || dayOpenNav <= 0)
            return false;

        var drawdownPct = (dayOpenNav - currentBalance) / dayOpenNav * 100m;

        if (drawdownPct < _maxDrawdownPct)
            return false;

        // Breached — set flag via SetNX so the warning log fires exactly once
        var justBreached = await _db.StringSetAsync(BreachedKey, "1", KeyTtl, When.NotExists);
        if (justBreached)
        {
            logger.LogWarning(
                "DAILY DRAWDOWN CIRCUIT BREAKER TRIPPED: {DrawdownPct:F2}% >= {MaxPct:F2}%. " +
                "Day-open NAV={DayOpenNav:C}, Current={Current:C}. " +
                "All new entries paused until UTC midnight ({Day}).",
                drawdownPct, _maxDrawdownPct, dayOpenNav, currentBalance, DayStamp);
        }

        return true;
    }

    public async Task<decimal?> GetDayOpenNavAsync(CancellationToken ct = default)
    {
        var navStr = await _db.StringGetAsync(NavKey);
        if (!navStr.HasValue)
            return null;

        return decimal.TryParse((string?)navStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var nav)
            ? nav
            : null;
    }
}
