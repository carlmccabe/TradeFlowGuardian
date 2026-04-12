using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Strategies.Builders;
using TradeFlowGuardian.Strategies.Signals.Crossover;
using TradeFlowGuardian.Strategies.Rules;

namespace TradeFlowGuardian.Backtesting.Strategies;

/// <summary>
/// Resolves named strategy presets into IStrategy instances backed by the composable pipeline.
/// Add new presets here as the strategy library grows — no changes required in controllers or the engine.
/// </summary>
public static class StrategyFactory
{
    /// <summary>
    /// Creates a strategy by preset name.
    /// </summary>
    /// <param name="strategyName">
    /// One of: emac_10_30, emac_9_21, emac_12_26, emac_custom.
    /// emac_custom requires <paramref name="fastPeriods"/> and <paramref name="slowPeriods"/>.
    /// </param>
    /// <param name="fastPeriods">Fast EMA period (only used for emac_custom).</param>
    /// <param name="slowPeriods">Slow EMA period (only used for emac_custom).</param>
    public static IStrategy Create(string strategyName, int? fastPeriods = null, int? slowPeriods = null)
    {
        return strategyName.ToLowerInvariant() switch
        {
            "emac_10_30" => BuildEmacCrossover("emac_10_30", 10, 30),
            "emac_9_21"  => BuildEmacCrossover("emac_9_21",   9, 21),
            "emac_12_26" => BuildEmacCrossover("emac_12_26", 12, 26),

            "emac_custom" => BuildEmacCrossover(
                $"emac_{fastPeriods}_{slowPeriods}",
                fastPeriods  ?? throw new ArgumentException("fastPeriods is required for emac_custom", nameof(fastPeriods)),
                slowPeriods  ?? throw new ArgumentException("slowPeriods is required for emac_custom", nameof(slowPeriods))),

            _ => throw new ArgumentException(
                $"Unknown strategy preset '{strategyName}'. " +
                "Supported: emac_10_30, emac_9_21, emac_12_26, emac_custom",
                nameof(strategyName))
        };
    }

    /// <summary>Returns the list of all supported preset names.</summary>
    public static IReadOnlyList<string> SupportedPresets =>
    [
        "emac_10_30",
        "emac_9_21",
        "emac_12_26",
        "emac_custom"
    ];

    // ── Builders ──────────────────────────────────────────────────────────────

    private static IStrategy BuildEmacCrossover(string id, int fast, int slow)
    {
        var signal = new EmaCrossoverSignal($"ema_cross_{fast}_{slow}", fast, slow);

        // The rule holds its own signal list — this is the correct pattern for FilteredSignalRule.
        var rule = new FilteredSignalRule(
            id: $"rule_{id}",
            name: $"EMA Crossover {fast}/{slow}",
            filters: [],
            signals: [signal],
            minConfidence: 0.5);

        var pipeline = new PipelineBuilder()
            .WithId(id)
            .AddSignal(signal)   // pipeline introspection / tracing
            .WithRule(rule)
            .Build();

        return new PipelineStrategy(pipeline, $"EMAC {fast}/{slow}");
    }
}
