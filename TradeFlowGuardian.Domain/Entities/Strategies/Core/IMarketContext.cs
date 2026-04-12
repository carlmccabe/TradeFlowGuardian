namespace TradeFlowGuardian.Domain.Entities.Strategies.Core;

/// <summary>
/// Immutable snapshot of market and account state at a specific point in time.
/// All evaluation functions receive this to ensure determinism.
/// </summary>
public interface IMarketContext
{
    /// <summary>Current UTC timestamp for this evaluation cycle</summary>
    DateTime TimestampUtc { get; }
    
    /// <summary>The instrument being evaluated (e.g., "EUR_USD")</summary>
    string Instrument { get; }
    
    /// <summary>Current account balance in account currency</summary>
    decimal AccountBalance { get; }
    
    /// <summary>Available margin for new positions</summary>
    decimal AvailableMargin { get; }
    
    /// <summary>Current open position (null if flat)</summary>
    Position? OpenPosition { get; }
    
    /// <summary>Historical candles (oldest to newest)</summary>
    IReadOnlyList<Candle> Candles { get; }
    
    /// <summary>Pre-computed indicators for this context</summary>
    IReadOnlyDictionary<string, IIndicatorResult> Indicators { get; }
    
    /// <summary>Correlation ID for tracing this evaluation through the pipeline</summary>
    string CorrelationId { get; }
    
    /// <summary>Custom metadata for strategy-specific context</summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
}