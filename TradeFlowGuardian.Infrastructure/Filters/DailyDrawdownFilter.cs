using Microsoft.Extensions.Logging;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Filters;

/// <summary>
/// Blocks new Long/Short entries when today's daily drawdown limit has been breached.
/// Close signals bypass this filter entirely (handled before filters run in SignalExecutionHandler).
/// State lives in Redis and resets automatically at UTC midnight.
/// </summary>
public class DailyDrawdownFilter(
    IDailyDrawdownGuard guard,
    ILogger<DailyDrawdownFilter> logger) : ISignalFilter
{
    public async Task<FilterResult> EvaluateAsync(TradeSignal signal, CancellationToken ct = default)
    {
        if (await guard.IsBreachedAsync(ct))
        {
            logger.LogWarning(
                "Signal for {Instrument} blocked: daily drawdown circuit breaker is active.",
                signal.Instrument);

            return FilterResult.Block(
                "Daily drawdown limit breached — entries paused until UTC midnight",
                "daily_drawdown");
        }

        return FilterResult.Allow();
    }
}
