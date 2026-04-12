using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Filters.Base;

namespace TradeFlowGuardian.Strategies.Filters;

public enum ComparisonOperator
{
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    Equal
}

/// <summary>
/// Filters based on RSI threshold
/// </summary>
public sealed class RsiThresholdFilter : FilterBase
{
    private readonly string _rsiIndicatorId;
    private readonly double _threshold;
    private readonly ComparisonOperator _operator;

    public RsiThresholdFilter(
        string id,
        string rsiIndicatorId,
        double threshold,
        ComparisonOperator op)
        : base(id, $"RSI {op} {threshold}")
    {
        _rsiIndicatorId = rsiIndicatorId ?? throw new ArgumentNullException(nameof(rsiIndicatorId));
        _threshold = threshold;
        _operator = op;
    }

    protected override FilterResult EvaluateCore(IMarketContext context)
    {
        if (!context.Indicators.TryGetValue(_rsiIndicatorId, out var indicatorResult))
        {
            return new FilterResult
            {
                Passed = false,
                Reason = $"RSI indicator '{_rsiIndicatorId}' not found",
                EvaluatedAt = context.TimestampUtc
            };
        }

        if (!indicatorResult.IsValid)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = $"RSI indicator invalid: {indicatorResult.ErrorReason}",
                EvaluatedAt = context.TimestampUtc
            };
        }

        var rsiValues = indicatorResult.Values;
        var currentRsi = rsiValues[^1].Value;

        if (!currentRsi.HasValue)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = "RSI value not available",
                EvaluatedAt = context.TimestampUtc
            };
        }

        bool passed = _operator switch
        {
            ComparisonOperator.LessThan => currentRsi.Value < _threshold,
            ComparisonOperator.LessThanOrEqual => currentRsi.Value <= _threshold,
            ComparisonOperator.GreaterThan => currentRsi.Value > _threshold,
            ComparisonOperator.GreaterThanOrEqual => currentRsi.Value >= _threshold,
            ComparisonOperator.Equal => Math.Abs(currentRsi.Value - _threshold) < 0.001,
            _ => false
        };

        return new FilterResult
        {
            Passed = passed,
            Reason = $"RSI={currentRsi.Value:F2} {(passed ? "passes" : "fails")} {_operator} {_threshold}",
            EvaluatedAt = context.TimestampUtc,
            Diagnostics = new Dictionary<string, object>
            {
                ["RSI"] = currentRsi.Value,
                ["Threshold"] = _threshold,
                ["Operator"] = _operator.ToString()
            }
        };
    }
}
