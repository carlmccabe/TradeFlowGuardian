using TradeFlowGuardian.Domain.Entities.Strategies.Core;

namespace TradeFlowGuardian.Strategies.Filters;

public sealed class SpreadFilter : IFilter
{
    // public string Name { get; }
    private decimal MaxSpread { get; }
    
    // public bool ShouldAllow(MarketContext context, SignalResult signal)
    // {
    //     return context.Spread <= MaxSpread;
    // }

    public SpreadFilter(decimal maxSpread)
    {
        MaxSpread = maxSpread;
        // Name = $"SpreadFilter({maxSpread})";
    }

    public string Id { get; }
    public string Description { get; }
    public FilterResult Evaluate(IMarketContext context)
    {
        throw new NotImplementedException();
    }
}