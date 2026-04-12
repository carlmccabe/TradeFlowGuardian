using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Filters.Base;

namespace TradeFlowGuardian.Strategies.Filters;

public enum TrendDirection
{
    Any,
    Up,
    Down
}

/// <summary>
/// Filters based on trend direction (price vs EMA)
/// </summary>
public sealed class TrendFilter : FilterBase
{
    private readonly string _emaIndicatorId;
    private readonly TrendDirection _requiredDirection;

    public TrendFilter(string id, string emaIndicatorId, TrendDirection requiredDirection)
        : base(id, $"Trend {requiredDirection}")
    {
        _emaIndicatorId = emaIndicatorId ?? throw new ArgumentNullException(nameof(emaIndicatorId));
        _requiredDirection = requiredDirection;
    }

    protected override FilterResult EvaluateCore(IMarketContext context)
    {
        if (!context.Indicators.TryGetValue(_emaIndicatorId, out var indicatorResult))
        {
            return new FilterResult
            {
                Passed = false,
                Reason = $"EMA indicator '{_emaIndicatorId}' not found",
                EvaluatedAt = context.TimestampUtc
            };
        }

        if (!indicatorResult.IsValid || context.Candles.Count == 0)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = "Indicator invalid or no candles",
                EvaluatedAt = context.TimestampUtc
            };
        }

        var emaValues =indicatorResult.Values;
        var currentEma = emaValues[^1].Value;
        var currentPrice = (double)context.Candles[^1].Close;

        if (!currentEma.HasValue)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = "EMA value not available",
                EvaluatedAt = context.TimestampUtc
            };
        }

        var actualDirection = currentPrice > currentEma.Value 
            ? TrendDirection.Up 
            : TrendDirection.Down;

        bool passed = actualDirection == _requiredDirection || _requiredDirection == TrendDirection.Any;

        return new FilterResult
        {
            Passed = passed,
            Reason = $"Price={currentPrice:F5}, EMA={currentEma.Value:F5}, Trend={actualDirection}",
            EvaluatedAt = context.TimestampUtc,
            Diagnostics = new Dictionary<string, object>
            {
                ["Price"] = currentPrice,
                ["EMA"] = currentEma.Value,
                ["ActualTrend"] = actualDirection.ToString(),
                ["RequiredTrend"] = _requiredDirection.ToString()
            }
        };
    }
}