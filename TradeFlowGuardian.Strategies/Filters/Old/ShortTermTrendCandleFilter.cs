using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;

namespace TradeFlowGuardian.Strategies.Filters;

/// <summary>
/// Confirms short-term candle trend by counting bullish/bearish bars.
/// </summary>
public sealed class ShortTermTrendFilter(int candleCount = 5, int minimumBullishCandles = 3) : IFilter
{
    // public string Name => $"ShortTermTrend({candleCount},{minimumBullishCandles})";
    //
    // public bool ShouldAllow(MarketContext context, SignalResult signal)
    // {
    //     if (signal.Action is TradeAction.Hold or TradeAction.Exit)
    //         return true;
    //
    //     if (context.Candles.Count < candleCount)
    //         return false;
    //
    //     var recent = context.Candles.Skip(Math.Max(0, context.Candles.Count - candleCount)).ToList();
    //     var bullish = recent.Count(c => c.Close > c.Open);
    //     var bearish = candleCount - bullish;
    //     var trendBullish = bullish >= minimumBullishCandles;
    //
    //     return signal.Action switch
    //     {
    //         TradeAction.Buy => trendBullish,
    //         TradeAction.Sell => !trendBullish && bearish >= minimumBullishCandles,
    //         _ => true
    //     };
    // }
    // public FilterResult Evaluate(MarketContext context, SignalResult signal)
    // {
    //     if (signal.Action is TradeAction.Hold or TradeAction.Exit)
    //         return FilterResult.Pass("No confirmation needed");
    //
    //     if (context.Candles.Count < candleCount)
    //         return FilterResult.Reject("Insufficient candles for trend analysis");
    //
    //     var recent = context.Candles.Skip(Math.Max(0, context.Candles.Count - candleCount)).ToList();
    //     var bullish = recent.Count(c => c.Close > c.Open);
    //     var bearish = candleCount - bullish;
    //     var trendBullish = bullish >= minimumBullishCandles;
    //
    //     if (signal.Action == TradeAction.Buy && trendBullish)
    //         return FilterResult.Pass($"Trend BULLISH: {bullish}/{candleCount} bullish");
    //
    //     if (signal.Action == TradeAction.Sell && !trendBullish && bearish >= minimumBullishCandles)
    //         return FilterResult.Pass($"Trend BEARISH: {bearish}/{candleCount} bearish");
    //
    //     var label = trendBullish ? "BULLISH" : "BEARISH";
    //     return FilterResult.Reject($"5-min trend {label} not aligned with {signal.Action}");
    // }
    public string Id { get; }
    public string Description { get; }
    public FilterResult Evaluate(IMarketContext context)
    {
        throw new NotImplementedException();
    }
}
