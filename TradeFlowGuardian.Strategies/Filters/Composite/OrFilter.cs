using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Filters.Base;

/// <summary>
/// Logical OR - at least one child filter must pass
/// </summary>
public sealed class OrFilter : FilterBase
{
    private readonly IReadOnlyList<IFilter> _filters;

    public OrFilter(string id, IReadOnlyList<IFilter> filters)
        : base(id, $"OR({filters.Count} filters)")
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

            if (result.Passed)
            {
                // Short-circuit on first success
                return new FilterResult
                {
                    Passed = true,
                    Reason = $"OR passed: {filter.Id} - {result.Reason}",
                    EvaluatedAt = DateTime.UtcNow,
                    Diagnostics = diagnostics
                };
            }
        }

        return new FilterResult
        {
            Passed = false,
            Reason = $"All {_filters.Count} filters failed",
            EvaluatedAt = DateTime.UtcNow,
            Diagnostics = diagnostics
        };
    }
}