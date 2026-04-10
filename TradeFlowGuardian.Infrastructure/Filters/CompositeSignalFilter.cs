using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Filters;

/// <summary>
/// Runs all registered filters in sequence.
/// Returns the first blocking result, or Allow if all pass.
/// Register filters in DI in priority order (cheapest checks first).
/// </summary>
public class CompositeSignalFilter : ISignalFilter
{
    private readonly IEnumerable<ISignalFilter> _filters;

    public CompositeSignalFilter(IEnumerable<ISignalFilter> filters) => _filters = filters;

    public async Task<FilterResult> EvaluateAsync(TradeSignal signal, CancellationToken ct = default)
    {
        foreach (var filter in _filters)
        {
            var result = await filter.EvaluateAsync(signal, ct);
            if (!result.Allowed)
                return result;
        }
        return FilterResult.Allow();
    }
}
