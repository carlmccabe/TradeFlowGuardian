namespace TradeFlowGuardian.Domain.Entities.Strategies.Core;

/// <summary>
/// Complete pipeline: data -> indicators -> filters -> signals -> rules -> decision
/// </summary>
public interface IPipeline
{
    /// <summary>Pipeline identifier</summary>
    string Id { get; }
    
    /// <summary>Registered indicators</summary>
    IReadOnlyList<IIndicator> Indicators { get; }
    
    /// <summary>Registered filters</summary>
    IReadOnlyList<IFilter> Filters { get; }
    
    /// <summary>Registered signals</summary>
    IReadOnlyList<ISignal> Signals { get; }
    
    /// <summary>Decision rule</summary>
    IRule Rule { get; }
    
    /// <summary>
    /// Execute full pipeline for given market state.
    /// </summary>
    PipelineResult Execute(
        IReadOnlyList<Candle> candles,
        IAccountState accountState,
        DateTime timestampUtc,
        bool enableTrace = false);
}

public sealed record PipelineResult
{
    public RuleDecision Decision { get; init; } = null!;
    public IReadOnlyDictionary<string, IIndicatorResult> IndicatorResults { get; init; } = 
        new Dictionary<string, IIndicatorResult>();
    public IReadOnlyList<FilterResult> FilterResults { get; init; } = Array.Empty<FilterResult>();
    public IReadOnlyList<SignalResult> SignalResults { get; init; } = Array.Empty<SignalResult>();
    public TimeSpan ExecutionTime { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
}

public interface IAccountState
{
    string Instrument { get; }
    decimal Balance { get; }
    decimal AvailableMargin { get; }
    Position? OpenPosition { get; }
}