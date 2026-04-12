using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;

namespace TradeFlowGuardian.Strategies.Filters;

/// <summary>
/// Allows trades only if the faster MA is above/below the slower MA in the
/// direction of the signal (long requires fast > slow; short requires fast  slow).
/// Uses current bar values; no "fresh cross" requirement (trend alignment only).
/// </summary>
public sealed class MovingAverageCrossFilter : IFilter
{
    public string Name => $"SMA{_fastPeriod}over{_slowPeriod}";
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;

    public MovingAverageCrossFilter(int fastPeriod, int slowPeriod)
    {
        if (fastPeriod <= 0 || slowPeriod <= 0) throw new ArgumentOutOfRangeException();
        if (fastPeriod >= slowPeriod) throw new ArgumentException("fastPeriod must be < slowPeriod");
        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;
    }

    // public bool ShouldAllow(MarketContext context, SignalResult signal)
    // {
    //     if (context.Candles.Count < _slowPeriod) return false;
    //
    //     var fast = context.SMA(_fastPeriod);
    //     var slow = context.SMA(_slowPeriod);
    //     if (fast is null || slow is null) return false;
    //
    //     var isLong = signal.Action == TradeAction.Buy;
    //     var isShort = signal.Action == TradeAction.Sell;
    //
    //     if (isLong) return fast.Value > slow.Value;
    //     if (isShort) return fast.Value < slow.Value;
    //     return false;
    // }

    public string Id { get; }
    public string Description { get; }
    public FilterResult Evaluate(IMarketContext context)
    {
        throw new NotImplementedException();
    }
}
