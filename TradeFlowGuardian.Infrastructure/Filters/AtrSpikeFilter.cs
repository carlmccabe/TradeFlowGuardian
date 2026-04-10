using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Filters;

/// <summary>
/// Blocks signals when ATR is spiking above its rolling average by a configurable multiplier.
/// This is the filter that would have blocked the Apr 10 USD/JPY tariff whipsaw.
///
/// TV Pine should include ATR in the webhook payload so we can validate server-side
/// without needing our own candle feed in Phase 1.
/// </summary>
public class AtrSpikeFilter : ISignalFilter
{
    private readonly FilterConfig _config;
    private readonly ILogger<AtrSpikeFilter> _logger;

    // Rolling ATR history per instrument — keyed by instrument string
    // Phase 2: move this to Redis so it survives restarts
    private static readonly Dictionary<string, Queue<decimal>> AtrHistory = new();
    private const int HistorySize = 20; // ~20 bars of ATR context

    public AtrSpikeFilter(IOptions<FilterConfig> config, ILogger<AtrSpikeFilter> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public Task<FilterResult> EvaluateAsync(TradeSignal signal, CancellationToken ct = default)
    {
        if (!_config.EnableAtrSpikeFilter || signal.Atr <= 0)
            return Task.FromResult(FilterResult.Allow());

        // Maintain rolling ATR history per instrument
        if (!AtrHistory.TryGetValue(signal.Instrument, out var history))
        {
            history = new Queue<decimal>();
            AtrHistory[signal.Instrument] = history;
        }

        history.Enqueue(signal.Atr);
        if (history.Count > HistorySize)
            history.Dequeue();

        // Need at least half the window before filtering kicks in
        if (history.Count < HistorySize / 2)
            return Task.FromResult(FilterResult.Allow());

        var rollingAvg = history.Average();
        var spikeThreshold = rollingAvg * _config.AtrSpikeMultiplier;

        if (signal.Atr > spikeThreshold)
        {
            _logger.LogWarning(
                "ATR spike detected on {Instrument}: current={Current:F5} avg={Avg:F5} threshold={Threshold:F5}",
                signal.Instrument, signal.Atr, rollingAvg, spikeThreshold);

            return Task.FromResult(FilterResult.Block(
                $"ATR spike: {signal.Atr:F5} > {spikeThreshold:F5} ({_config.AtrSpikeMultiplier}× avg)",
                "atr_spike"));
        }

        return Task.FromResult(FilterResult.Allow());
    }
}
