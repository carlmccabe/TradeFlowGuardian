namespace TradeFlowGuardian.Domain.Entities.Strategies.Core;

/// <summary>
/// Combines signals and filters to produce actionable trading decisions.
/// This is where strategy logic lives.
/// </summary>
public interface IRule
{
    /// <summary>Rule identifier</summary>
    string Id { get; }
    
    /// <summary>Rule name</summary>
    string Name { get; }
    
    /// <summary>
    /// Evaluate rule and produce a trading decision.
    /// Should check filters first, then evaluate signals.
    /// </summary>
    RuleDecision Evaluate(IMarketContext context);
}

/// <summary>
/// Trading decision from a rule evaluation
/// </summary>
public sealed record RuleDecision
{
    /// <summary>Action to take</summary>
    public TradeAction Action { get; init; }
    
    /// <summary>Decision confidence [0.0, 1.0]</summary>
    public double Confidence { get; init; }
    
    /// <summary>Reasons for this decision (from signals/filters)</summary>
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
    
    /// <summary>Suggested position size (units)</summary>
    public long? PositionSize { get; init; }
    
    /// <summary>Stop loss price (null if not applicable)</summary>
    public decimal? StopLoss { get; init; }
    
    /// <summary>Take profit price (null if not applicable)</summary>
    public decimal? TakeProfit { get; init; }
    
    /// <summary>Full evaluation trace (for debugging/explain mode)</summary>
    public EvaluationTrace? Trace { get; init; }
    
    /// <summary>Timestamp of decision</summary>
    public DateTime DecidedAt { get; init; }
}

public enum TradeAction
{
    Hold = 0,
    EnterLong = 1,
    EnterShort = -1,
    ExitPosition = 2
}

/// <summary>
/// Complete evaluation trace for explain mode
/// </summary>
public sealed class EvaluationTrace
{
    public IReadOnlyList<FilterResult> FilterResults { get; set; } = Array.Empty<FilterResult>();
    public IReadOnlyList<SignalResult> SignalResults { get; set; } = Array.Empty<SignalResult>();
    public Dictionary<string, object> Metadata { get; set; } = new();
}