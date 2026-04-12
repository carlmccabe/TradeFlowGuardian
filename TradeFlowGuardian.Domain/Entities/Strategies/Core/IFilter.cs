namespace TradeFlowGuardian.Domain.Entities.Strategies.Core;

/// <summary>
/// Evaluates whether market conditions pass/fail a criterion.
/// Used to gate signal generation (e.g., "only trade during London session").
/// </summary>
public interface IFilter
{
    /// <summary>Unique identifier for this filter</summary>
    string Id { get; }
    
    /// <summary>Human-readable description</summary>
    string Description { get; }
    
    /// <summary>
    /// Evaluate filter against current market context.
    /// Should be fast - this may be called frequently.
    /// </summary>
    FilterResult Evaluate(IMarketContext context);
}

/// <summary>
/// Result of a filter evaluation with reasoning
/// </summary>
public sealed record FilterResult
{
    public bool Passed { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime EvaluatedAt { get; init; }
    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } = 
        new Dictionary<string, object>();
}