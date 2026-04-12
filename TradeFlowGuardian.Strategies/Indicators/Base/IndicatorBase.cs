using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;

namespace TradeFlowGuardian.Strategies.Indicators.Base;

/// <summary>
/// Base class for all indicator implementations providing common functionality
/// </summary>
public abstract class IndicatorBase : IIndicator
{
    protected IndicatorBase(string id, string name, int warmupPeriod)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        WarmupPeriod = warmupPeriod;
    }

    public string Id { get; }
    public string Name { get; }
    public int WarmupPeriod { get; }

    public IIndicatorResult Compute(IReadOnlyList<Candle> candles)
    {
        // Allow computation with less than warmup period - just return empty/partial results
        if (candles == null)
        {
            throw new ArgumentNullException(nameof(candles));
        }

        if (candles.Count == 0)
        {
            return IndicatorResult.Success(Id, new List<IndicatorValue>());
        }

        // If insufficient data, still try to compute (will return nulls for warm-up period)
        if (candles.Count < WarmupPeriod)
        {
            return IndicatorResult.Success(Id, new List<IndicatorValue>());
        }

        try
        {
            return ComputeCore(candles);
        }
        catch (Exception ex)
        {
            return IndicatorResult.Error(Id, ex.Message);
        }
    }

    protected abstract IIndicatorResult ComputeCore(IReadOnlyList<Candle> candles);

    /// <summary>
    /// Extract price series from candles based on source
    /// </summary>
    protected static IReadOnlyList<double> ExtractPrices(
        IReadOnlyList<Candle> candles,
        PriceSource source)
    {
        return source switch
        {
            PriceSource.Open => candles.Select(c => (double)c.Open).ToList(),
            PriceSource.High => candles.Select(c => (double)c.High).ToList(),
            PriceSource.Low => candles.Select(c => (double)c.Low).ToList(),
            PriceSource.Close => candles.Select(c => (double)c.Close).ToList(),
            PriceSource.HL2 => candles.Select(c => (double)((c.High + c.Low) / 2)).ToList(),
            PriceSource.HLC3 => candles.Select(c => (double)((c.High + c.Low + c.Close) / 3)).ToList(),
            PriceSource.OHLC4 => candles.Select(c => (double)((c.Open + c.High + c.Low + c.Close) / 4)).ToList(),
            _ => throw new ArgumentException($"Unknown price source: {source}")
        };
    }
}

public enum PriceSource
{
    Open,
    High,
    Low,
    Close,
    HL2, // (High + Low) / 2
    HLC3, // (High + Low + Close) / 3
    OHLC4 // (Open + High + Low + Close) / 4
}

/// <summary>
/// Concrete implementation of IIndicatorResult
/// </summary>
public sealed class IndicatorResult : IIndicatorResult
{
    public bool IsValid { get; init; }
    public string? ErrorReason { get; init; }
    public IReadOnlyList<IndicatorValue> Values { get; init; } = Array.Empty<IndicatorValue>();

    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } =
        new Dictionary<string, object>();

    public DateTime ComputedAt { get; init; }

    public static IIndicatorResult Success(
        string indicatorId,
        List<IndicatorValue> values,
        Dictionary<string, object>? diagnostics = null)
    {
        return new IndicatorResult
        {
            IsValid = true,
            Values = values,
            Diagnostics = diagnostics ?? new Dictionary<string, object>(),
            ComputedAt = DateTime.UtcNow
        };
    }

    public static IIndicatorResult InsufficientData(string indicatorId, string reason)
    {
        return new IndicatorResult
        {
            IsValid = false,
            ErrorReason = reason,
            Values = [],
            ComputedAt = DateTime.UtcNow,
            Diagnostics = new Dictionary<string, object>
            {
                ["IndicatorId"] = indicatorId,
                ["ErrorType"] = "InsufficientData"
            }
        };
    }

    public static IIndicatorResult Error(string indicatorId, string errorMessage)
    {
        return new IndicatorResult
        {
            IsValid = false,
            ErrorReason = errorMessage,
            Values = [],
            ComputedAt = DateTime.UtcNow,
            Diagnostics = new Dictionary<string, object>
            {
                ["IndicatorId"] = indicatorId,
                ["ErrorType"] = "ComputationError"
            }
        };
    }
}