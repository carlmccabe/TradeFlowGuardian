using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Filters;

/// <summary>
/// Runs all registered filters in sequence.
/// Returns the first blocking result, or Allow if all pass.
/// Register filters in DI in priority order (cheapest checks first).
/// </summary>
public class CompositeSignalFilter(IEnumerable<ISignalFilter> filters) : ISignalFilter
{
    public async Task<FilterResult> EvaluateAsync(TradeSignal signal, CancellationToken ct = default)
    {
        foreach (var filter in filters)
        {
            var result = await filter.EvaluateAsync(signal, ct);
            if (!result.Allowed)
                return result;
        }
        return FilterResult.Allow();
    }
}
