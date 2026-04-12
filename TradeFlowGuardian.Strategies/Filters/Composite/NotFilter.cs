using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Filters.Base;

namespace TradeFlowGuardian.Strategies.Filters.Composite;

/// <summary>
/// Logical NOT - inverts child filter result
/// </summary>
public sealed class NotFilter : FilterBase
{
    private readonly IFilter _filter;

    public NotFilter(string id, IFilter filter)
        : base(id, $"NOT({filter.Id})")
    {
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    protected override FilterResult EvaluateCore(IMarketContext context)
    {
        var result = _filter.Evaluate(context);

        return new FilterResult
        {
            Passed = !result.Passed,
            Reason = $"NOT: {result.Reason}",
            EvaluatedAt = DateTime.UtcNow,
            Diagnostics = new Dictionary<string, object>
            {
                ["InnerResult"] = result
            }
        };
    }
}