// File: src/TradeFlowGuardian.Domain/Entities/Strategies/Core/IIndicator.cs

namespace TradeFlowGuardian.Domain.Entities.Strategies.Core;

/// <summary>
/// Computes a technical indicator from price/volume data.
/// Must be pure and deterministic - no I/O, no clock access.
/// </summary>
public interface IIndicator
{
    /// <summary>Unique identifier for this indicator (e.g., "SMA_20")</summary>
    string Id { get; }
    
    /// <summary>Human-readable name</summary>
    string Name { get; }
    
    /// <summary>Minimum number of candles required for valid computation</summary>
    int WarmupPeriod { get; }

    /// <summary>
    /// Compute indicator value(s) for the given candle series.
    /// Returns InsufficientData if candles.Count < WarmupPeriod. />
    /// </summary>
    IIndicatorResult Compute(IReadOnlyList<Candle> candles);
}

/// <summary>
/// Result of an indicator computation with diagnostic information
/// </summary>
public interface IIndicatorResult
{
    /// <summary>Whether computation succeeded</summary>
    bool IsValid { get; }
    
    /// <summary>Reason for failure if !IsValid</summary>
    string? ErrorReason { get; }

    /// <summary>
    /// Collection of timestamped indicator values
    /// </summary>
    IReadOnlyList<IndicatorValue> Values { get; }
    
    /// <summary>Diagnostic information for debugging/logging</summary>
    IReadOnlyDictionary<string, object> Diagnostics { get; }
    
    /// <summary>Timestamp when this was computed</summary>
    DateTime ComputedAt { get; }
}

/// <summary>
/// Single indicator value with timestamp
/// </summary>
public record IndicatorValue
{
    /// <summary>
    /// The indicator value (null during warm-up period)
    /// </summary>
    public double? Value { get; init; }

    /// <summary>
    /// Timestamp of the candle this value corresponds to
    /// </summary>
    public DateTime Timestamp { get; init; }
}