using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Builders;
using TradeFlowGuardian.Strategies.Filters;
using TradeFlowGuardian.Strategies.Signals.Crossover;
using TradeFlowGuardian.Strategies.Signals.Tfg;
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
    /// One of: emac_10_30, emac_9_21, emac_12_26, emac_custom, tfg_usdjpy_v5.
    /// emac_custom requires <paramref name="fastPeriods"/> and <paramref name="slowPeriods"/>.
    /// tfg_usdjpy_v5 accepts optional <paramref name="slMultiplier"/> / <paramref name="tpMultiplier"/>
    /// overrides (defaults 2.6 / 5.3) for stop-width sweeps.
    /// </param>
    /// <param name="fastPeriods">Fast EMA period (only used for emac_custom).</param>
    /// <param name="slowPeriods">Slow EMA period (only used for emac_custom).</param>
    /// <param name="slMultiplier">Stop-loss ATR multiplier (only used for tfg presets).</param>
    /// <param name="tpMultiplier">Take-profit ATR multiplier (only used for tfg presets).</param>
    public static IStrategy Create(
        string strategyName,
        int? fastPeriods = null,
        int? slowPeriods = null,
        decimal? slMultiplier = null,
        decimal? tpMultiplier = null)
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

            "tfg_usdjpy_v5" => BuildTfgV5(
                "tfg_usdjpy_v5",
                slMultiplier ?? 2.6m,
                tpMultiplier ?? 5.3m),

            _ => throw new ArgumentException(
                $"Unknown strategy preset '{strategyName}'. " +
                "Supported: emac_10_30, emac_9_21, emac_12_26, emac_custom, tfg_usdjpy_v5",
                nameof(strategyName))
        };
    }

    /// <summary>Returns the list of all supported preset names.</summary>
    public static IReadOnlyList<string> SupportedPresets =>
    [
        "emac_10_30",
        "emac_9_21",
        "emac_12_26",
        "emac_custom",
        "tfg_usdjpy_v5"
    ];

    // ── Builders ──────────────────────────────────────────────────────────────

    /// <summary>
    /// TFG v5 USDJPY — 1:1 port of pine/TFG Live &amp; Strategies/TFG_USDJPY_live.pine.
    /// M15: SMA 9/25 cross, EMA 179 trend, RSI 18, ATR 13 (SL/TP), EMA dist 5–69 pips,
    /// session 00–09 + 11–12 UTC. SL/TP multipliers parameterised for sweeps.
    /// </summary>
    private static IStrategy BuildTfgV5(string id, decimal slMult, decimal tpMult)
    {
        var signal = new TfgV5Signal($"{id}_signal", slMult: slMult, tpMult: tpMult);

        // Pine: inSession = hour ∈ [0, 9) or [11, 12) UTC, evaluated on bar-open time.
        // End times are :59:59 because TimeFilter's range check is inclusive.
        var session = new OrFilter($"{id}_session",
        [
            new TimeFilter($"{id}_session_0009", new TimeSpan(0, 0, 0),  new TimeSpan(8, 59, 59)),
            new TimeFilter($"{id}_session_1112", new TimeSpan(11, 0, 0), new TimeSpan(11, 59, 59))
        ]);

        var rule = new FilteredSignalRule(
            id: $"rule_{id}",
            name: $"TFG v5 USDJPY (SL {slMult}× / TP {tpMult}× ATR)",
            filters: [session],
            signals: [signal],
            minConfidence: 0.5);

        var pipeline = new PipelineBuilder()
            .WithId(id)
            .AddSignal(signal)
            .WithRule(rule)
            .Build();

        return new PipelineStrategy(pipeline, $"TFG v5 USDJPY SL{slMult}x TP{tpMult}x");
    }

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
