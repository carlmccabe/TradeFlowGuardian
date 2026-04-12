using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Pipeline;

namespace TradeFlowGuardian.Strategies.Builders;

/// <summary>
/// Fluent API for building pipelines
/// </summary>
public sealed class PipelineBuilder
{
    private string _id = Guid.NewGuid().ToString();
    private readonly List<IIndicator> _indicators = [];
    private readonly List<IFilter> _filters = [];
    private readonly List<ISignal> _signals = [];
    private IRule? _rule;

    public PipelineBuilder WithId(string id)
    {
        _id = id ?? throw new ArgumentNullException(nameof(id));
        return this;
    }

    public PipelineBuilder AddIndicator(IIndicator indicator)
    {
        _indicators.Add(indicator ?? throw new ArgumentNullException(nameof(indicator)));
        return this;
    }

    public PipelineBuilder AddFilter(IFilter filter)
    {
        _filters.Add(filter ?? throw new ArgumentNullException(nameof(filter)));
        return this;
    }

    public PipelineBuilder AddSignal(ISignal signal)
    {
        _signals.Add(signal ?? throw new ArgumentNullException(nameof(signal)));
        return this;
    }

    public PipelineBuilder WithRule(IRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        return this;
    }

    public IPipeline Build()
    {
        if (_rule == null)
            throw new InvalidOperationException("Rule is required");

        if (_signals.Count == 0)
            throw new InvalidOperationException("At least one signal is required");

        return new StandardPipeline(_id, _indicators, _filters, _signals, _rule);
    }
}