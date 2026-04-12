// File: src/TradeFlowGuardian.Strategies/Filters/Base/FilterBase.cs

using TradeFlowGuardian.Domain.Entities.Strategies.Core;

namespace TradeFlowGuardian.Strategies.Filters.Base;

public abstract class FilterBase : IFilter
{
    protected FilterBase(string id, string description)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public string Id { get; }
    public string Description { get; }

    public FilterResult Evaluate(IMarketContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            return EvaluateCore(context);
        }
        catch (Exception ex)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = $"Filter error: {ex.Message}",
                EvaluatedAt = DateTime.UtcNow,
                Diagnostics = new Dictionary<string, object>
                {
                    ["Exception"] = ex.ToString()
                }
            };
        }
    }

    protected abstract FilterResult EvaluateCore(IMarketContext context);
}