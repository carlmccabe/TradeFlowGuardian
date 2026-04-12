using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using DomainTradeAction = TradeFlowGuardian.Domain.Entities.TradeAction;
using PipelineTradeAction = TradeFlowGuardian.Domain.Entities.Strategies.Core.TradeAction;

namespace TradeFlowGuardian.Backtesting.Strategies;

/// <summary>
/// Adapts an IPipeline (indicator → filter → signal → rule) to the IStrategy contract
/// expected by BacktestEngine. Translates pipeline TradeAction values to the engine's
/// Buy/Sell/Exit/Hold enum.
/// </summary>
public sealed class PipelineStrategy(IPipeline pipeline, string name) : IStrategy
{
    public string Name => name;

    public Decision Evaluate(
        IReadOnlyList<Candle> candles,
        DateTime nowUtc,
        bool hasOpenPosition,
        bool isLongPosition)
    {
        if (candles.Count == 0)
            return new Decision(DomainTradeAction.Hold, Reason: "No candle data");

        var accountState = new BacktestAccountState(
            instrument: candles.Last().Instrument,
            hasOpenPosition: hasOpenPosition,
            isLongPosition: isLongPosition,
            currentPrice: candles.Last().Close);

        var result = pipeline.Execute(candles, accountState, nowUtc);
        var decision = result.Decision;
        var reasons = string.Join("; ", decision.Reasons);

        return decision.Action switch
        {
            PipelineTradeAction.EnterLong => new Decision(
                DomainTradeAction.Buy,
                decision.StopLoss,
                decision.TakeProfit,
                reasons),

            PipelineTradeAction.EnterShort => new Decision(
                DomainTradeAction.Sell,
                decision.StopLoss,
                decision.TakeProfit,
                reasons),

            PipelineTradeAction.ExitPosition => new Decision(
                DomainTradeAction.Exit,
                Reason: reasons),

            _ => new Decision(DomainTradeAction.Hold)
        };
    }

    /// <summary>
    /// Minimal IAccountState implementation that feeds the pipeline with position context
    /// from the backtest engine's state — balance/margin are not meaningful in simulation.
    /// </summary>
    private sealed class BacktestAccountState : IAccountState
    {
        public BacktestAccountState(
            string instrument,
            bool hasOpenPosition,
            bool isLongPosition,
            decimal currentPrice)
        {
            Instrument = instrument;
            OpenPosition = hasOpenPosition
                ? new Position
                {
                    Instrument = instrument,
                    Units = isLongPosition ? 1 : -1,
                    AveragePrice = currentPrice,
                    Side = isLongPosition ? "long" : "short"
                }
                : null;
        }

        public string Instrument { get; }
        public decimal Balance => 0m;
        public decimal AvailableMargin => 0m;
        public Position? OpenPosition { get; }
    }
}
