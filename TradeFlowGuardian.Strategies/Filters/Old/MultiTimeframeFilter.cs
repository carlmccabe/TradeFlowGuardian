using TradeFlowGuardian.Domain.Entities.Strategies.Core;

namespace TradeFlowGuardian.Strategies.Filters;

/// <summary>
/// Delegates filtering to another filter but evaluated on a higher (or different) timeframe.
/// The engine/context must support requesting higher-timeframe context snapshots.
/// </summary>
public sealed class MultiTimeframeFilter : IFilter
{
    // public string Name => $"MTF[{_targetTimeframe}]::{_inner.Name}";
    private readonly string _targetTimeframe;
    private readonly IFilter _inner;
    private readonly int _lookbackBars;

    /// <param name="targetTimeframe">e.g., "W1", "D1", "H4"</param>
    /// <param name="inner">Filter to be evaluated on the target timeframe</param>
    /// <param name="lookbackBars">How many target-timeframe bars needed to evaluate</param>
    public MultiTimeframeFilter(string targetTimeframe, IFilter inner, int lookbackBars = 150)
    {
        _targetTimeframe = targetTimeframe ?? throw new ArgumentNullException(nameof(targetTimeframe));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _lookbackBars = Math.Max(1, lookbackBars);
    }

    // public bool ShouldAllow(MarketContext context, SignalResult signal)
    // {
    //     // Ask the context to provide a higher-timeframe snapshot for the same instrument/time
    //     var higherTf = context.GetHigherTimeframeContext(_targetTimeframe, _lookbackBars);
    //     if (higherTf == null) return false;
    //     // Reuse same signal direction; some inner filters may ignore it
    //     return _inner.ShouldAllow(higherTf, signal);
    // }
    public string Id { get; }
    public string Description { get; }
    public FilterResult Evaluate(IMarketContext context)
    {
        throw new NotImplementedException();
    }
}
