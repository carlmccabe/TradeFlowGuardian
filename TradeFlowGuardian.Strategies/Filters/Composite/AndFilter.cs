using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Filters.Base;

namespace TradeFlowGuardian.Strategies.Filters.composite;

/// <summary>
/// Logical AND - all child filters must pass
/// </summary>
public sealed class AndFilter : FilterBase
{
    private readonly IReadOnlyList<IFilter> _filters;

    public AndFilter(string id, IReadOnlyList<IFilter> filters)
        : base(id, $"AND({filters.Count} filters)")
    {
        _filters = filters ?? throw new ArgumentNullException(nameof(filters));
        if (_filters.Count == 0)
            throw new ArgumentException("Must have at least one filter", nameof(filters));
    }

    protected override FilterResult EvaluateCore(IMarketContext context)
    {
        var diagnostics = new Dictionary<string, object>();

        foreach (var filter in _filters)
        {
            var result = filter.Evaluate(context);
            diagnostics[$"Filter_{filter.Id}"] = result;

            if (!result.Passed)
            {
                // Short-circuit on first failure
                return new FilterResult
                {
                    Passed = false,
                    Reason = $"AND failed: {filter.Id} - {result.Reason}",
                    EvaluatedAt = DateTime.UtcNow,
                    Diagnostics = diagnostics
                };
            }
        }

        return new FilterResult
        {
            Passed = true,
            Reason = $"All {_filters.Count} filters passed",
            EvaluatedAt = DateTime.UtcNow,
            Diagnostics = diagnostics
        };
    }
}
