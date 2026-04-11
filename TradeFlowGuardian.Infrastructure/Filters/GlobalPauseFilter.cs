using Microsoft.Extensions.Logging;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Filters;

/// <summary>
/// Blocks all new Long/Short entries when the global pause flag is set.
/// Close signals are unaffected (handled before filters run in SignalExecutionHandler).
/// </summary>
public class GlobalPauseFilter(
    IPauseState pauseState,
    ILogger<GlobalPauseFilter> logger) : ISignalFilter
{
    public async Task<FilterResult> EvaluateAsync(TradeSignal signal, CancellationToken ct = default)
    {
        if (await pauseState.IsPausedAsync(ct))
        {
            logger.LogWarning(
                "Signal for {Instrument} blocked: trading is globally paused.", signal.Instrument);
            return FilterResult.Block("Trading paused by operator", "paused");
        }

        return FilterResult.Allow();
    }
}
