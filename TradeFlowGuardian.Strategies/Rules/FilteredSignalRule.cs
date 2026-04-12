using TradeFlowGuardian.Domain.Entities.Strategies.Core;

namespace TradeFlowGuardian.Strategies.Rules;

/// <summary>
/// Simple rule: all filters must pass, then take highest confidence signal
/// </summary>
public sealed class FilteredSignalRule : IRule
{
    private readonly IReadOnlyList<IFilter> _filters;
    private readonly IReadOnlyList<ISignal> _signals;
    private readonly double _minConfidence;

    public FilteredSignalRule(
        string id,
        string name,
        IReadOnlyList<IFilter> filters,
        IReadOnlyList<ISignal> signals,
        double minConfidence = 0.5)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _filters = filters ?? throw new ArgumentNullException(nameof(filters));
        _signals = signals ?? throw new ArgumentNullException(nameof(signals));
        _minConfidence = minConfidence;
    }

    public string Id { get; }
    public string Name { get; }

    public RuleDecision Evaluate(IMarketContext context)
    {
        var reasons = new List<string>();
        var trace = new EvaluationTrace();

        // Step 1: Check if we should exit existing position
        if (context.OpenPosition != null)
        {
            var exitSignals = _signals.Select(s => s.Generate(context)).ToList();
            trace.SignalResults = exitSignals;

            var oppositeSignal = exitSignals.FirstOrDefault(s =>
                (context.OpenPosition.IsLong && s.Direction == SignalDirection.Short) ||
                (!context.OpenPosition.IsLong && s.Direction == SignalDirection.Long));

            if (oppositeSignal != null && oppositeSignal.Confidence >= _minConfidence)
            {
                return new RuleDecision
                {
                    Action = TradeAction.ExitPosition,
                    Confidence = oppositeSignal.Confidence,
                    Reasons = new[] { oppositeSignal.Reason },
                    Trace = trace,
                    DecidedAt = context.TimestampUtc
                };
            }

            return new RuleDecision
            {
                Action = TradeAction.Hold,
                Confidence = 1.0,
                Reasons = new[] { "Holding existing position" },
                Trace = trace,
                DecidedAt = context.TimestampUtc
            };
        }

        // Step 2: Evaluate filters
        var filterResults = new List<FilterResult>();
        foreach (var filter in _filters)
        {
            var result = filter.Evaluate(context);
            filterResults.Add(result);

            if (!result.Passed)
            {
                trace.FilterResults = filterResults;
                return new RuleDecision
                {
                    Action = TradeAction.Hold,
                    Confidence = 0.0,
                    Reasons = new[] { $"Filter failed: {result.Reason}" },
                    Trace = trace,
                    DecidedAt = context.TimestampUtc
                };
            }

            reasons.Add($"Filter passed: {result.Reason}");
        }
        trace.FilterResults = filterResults;

        // Step 3: Generate signals
        var signalResults = _signals.Select(s => s.Generate(context)).ToList();
        trace.SignalResults = signalResults;

        // Find highest confidence signal
        var bestSignal = signalResults
            .Where(s => s.Direction != SignalDirection.Neutral && s.Confidence >= _minConfidence)
            .OrderByDescending(s => s.Confidence)
            .FirstOrDefault();

        if (bestSignal == null)
        {
            return new RuleDecision
            {
                Action = TradeAction.Hold,
                Confidence = 0.0,
                Reasons = new[] { "No signals above minimum confidence" },
                Trace = trace,
                DecidedAt = context.TimestampUtc
            };
        }

        reasons.Add(bestSignal.Reason);

        var action = bestSignal.Direction == SignalDirection.Long 
            ? TradeAction.EnterLong 
            : TradeAction.EnterShort;

        return new RuleDecision
        {
            Action = action,
            Confidence = bestSignal.Confidence,
            Reasons = reasons,
            StopLoss = bestSignal.SuggestedStopLoss,
            TakeProfit = bestSignal.SuggestedTakeProfit,
            Trace = trace,
            DecidedAt = context.TimestampUtc
        };
    }
}