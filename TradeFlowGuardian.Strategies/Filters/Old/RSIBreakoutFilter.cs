using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Indicators;

namespace TradeFlowGuardian.Strategies.Filters;

/// <summary>
/// Confirms breakout entries using RSI with momentum and neutral band.
/// - Long allowed if RSI > longThreshold and rising
/// - Short allowed if RSI shortThreshold and falling
/// - Reject signals in neutral band (e.g., 45–55)
/// </summary>
public sealed class RSIBreakoutFilter(
    int rsiPeriod = 14,
    decimal longThreshold = 55m,
    decimal shortThreshold = 45m,
    decimal momentumEps = 0.5m) : IFilter
{
    // public string Name => $"RSI({rsiPeriod},{longThreshold:F0}/{shortThreshold:F0})";
    // private readonly RsiIndicator _rsi = new(rsiPeriod);

    // public bool ShouldAllow(MarketContext context, SignalResult signal)
    // {
    //     // Only confirm entries
    //     if (signal.Action is TradeAction.Hold or TradeAction.Exit)
    //         return true;
    //
    //     if (context.Candles.Count <= _rsi.Period + 1)
    //         return false;
    //
    //     var keyNow = $"{Name}_RSI_now";
    //     var keyPrev = $"{Name}_RSI_prev";
    //
    //     var rsiNow = context.GetOrCalculate(keyNow, () => _rsi.Calculate(context.Candles));
    //     var prevCandles = context.Candles.Take(context.Candles.Count - 1).ToList();
    //     var rsiPrev = context.GetOrCalculate(keyPrev, () => _rsi.Calculate(prevCandles));
    //
    //     // Neutral band filter
    //     if (rsiNow is >= 45m and <= 55m) return false;
    //
    //     var rising = rsiNow > rsiPrev + momentumEps;
    //     var falling = rsiNow < rsiPrev - momentumEps;
    //
    //     return signal.Action switch
    //     {
    //         TradeAction.Buy => rsiNow > longThreshold && rising,
    //         TradeAction.Sell => rsiNow < shortThreshold && falling,
    //         _ => true
    //     };
    // }

    public string Id { get; }
    public string Description { get; }
    public FilterResult Evaluate(IMarketContext context)
    {
        throw new NotImplementedException();
    }
}