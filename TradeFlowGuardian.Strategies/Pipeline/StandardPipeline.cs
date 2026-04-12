

using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeAction = TradeFlowGuardian.Domain.Entities.Strategies.Core.TradeAction;

namespace TradeFlowGuardian.Strategies.Pipeline;

public sealed class StandardPipeline : IPipeline
{
    private readonly IReadOnlyList<IIndicator> _indicators;
    private readonly IReadOnlyList<IFilter> _filters;
    private readonly IReadOnlyList<ISignal> _signals;
    private readonly IRule _rule;

    public StandardPipeline(
        string id,
        IReadOnlyList<IIndicator> indicators,
        IReadOnlyList<IFilter> filters,
        IReadOnlyList<ISignal> _signals,
        IRule rule)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        _indicators = indicators ?? throw new ArgumentNullException(nameof(indicators));
        _filters = filters ?? throw new ArgumentNullException(nameof(filters));
        this._signals = _signals ?? throw new ArgumentNullException(nameof(_signals));
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
    }

    public string Id { get; }
    public IReadOnlyList<IIndicator> Indicators => _indicators;
    public IReadOnlyList<IFilter> Filters => _filters;
    public IReadOnlyList<ISignal> Signals => _signals;
    public IRule Rule => _rule;

    public PipelineResult Execute(
        IReadOnlyList<Candle> candles,
        IAccountState accountState,
        DateTime timestampUtc,
        bool enableTrace = false)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            // Step 1: Compute all indicators
            var indicatorResults = new Dictionary<string, IIndicatorResult>();
            foreach (var indicator in _indicators)
            {
                var result = indicator.Compute(candles);
                indicatorResults[indicator.Id] = result;
            }

            // Step 2: Build market context
            var context = new MarketContext(
                timestampUtc,
                accountState.Instrument,
                accountState.Balance,
                accountState.AvailableMargin,
                accountState.OpenPosition,
                candles,
                indicatorResults,
                correlationId);

            // Step 3: Evaluate rule (which internally evaluates filters and signals)
            var decision = _rule.Evaluate(context);

            stopwatch.Stop();

            return new PipelineResult
            {
                Decision = decision,
                IndicatorResults = indicatorResults,
                FilterResults = decision.Trace?.FilterResults ?? Array.Empty<FilterResult>(),
                SignalResults = decision.Trace?.SignalResults ?? Array.Empty<SignalResult>(),
                ExecutionTime = stopwatch.Elapsed,
                CorrelationId = correlationId
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new PipelineResult
            {
                Decision = new RuleDecision
                {
                    Action = TradeAction.Hold,
                    Confidence = 0.0,
                    Reasons = new[] { $"Pipeline error: {ex.Message}" },
                    DecidedAt = timestampUtc
                },
                ExecutionTime = stopwatch.Elapsed,
                CorrelationId = correlationId
            };
        }
    }
}