using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Pipeline;

namespace TradeFlowGuardian.Strategies.Filters;

/// <summary>
/// Caps the number of entries per calendar month to reduce frequency.
/// Counts only entries tagged with the current strategy name (from context).
/// </summary>
public sealed class MaxMonthlyTradesFilter : IFilter
{
    public string Name => $"MaxMonthlyTrades({_maxPerMonth})";
    private readonly int _maxPerMonth;

    public MaxMonthlyTradesFilter(int maxPerMonth)
    {
        if (maxPerMonth <= 0) throw new ArgumentOutOfRangeException(nameof(maxPerMonth));
        _maxPerMonth = maxPerMonth;
    }

    // public bool ShouldAllow(MarketContext context, SignalResult signal)
    // {
    //     var now = context.CurrentTime;
    //     var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    //     var monthEnd = monthStart.AddMonths(1);
    //
    //     // Context should expose recent executions/trades for the instrument/strategy
    //     var trades = context.GetExecutedTrades(
    //         instrument: context.Instrument,
    //         fromUtc: monthStart,
    //         toUtc: monthEnd,
    //         strategyName: context.StrategyName);
    //
    //     var entriesThisMonth = trades.Count(t => t.IsEntry);
    //
    //     return entriesThisMonth < _maxPerMonth;
    // }

    public string Id { get; }
    public string Description { get; }
    public FilterResult Evaluate(IMarketContext context)
    {
        throw new NotImplementedException();
    }
}
