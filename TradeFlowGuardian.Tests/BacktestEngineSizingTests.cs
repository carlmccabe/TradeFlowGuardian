using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TradeFlowGuardian.Backtesting.Data;
using TradeFlowGuardian.Backtesting.Engine;
using TradeFlowGuardian.Backtesting.Models;
using TradeFlowGuardian.Domain.Entities;
using Xunit;

namespace TradeFlowGuardian.Tests;

/// <summary>
/// Proves the backtest engine sizes and books P&amp;L the way the live system would:
/// margin-capped units (shared PositionSizeCalculator) and account-currency P&amp;L.
/// </summary>
public class BacktestEngineSizingTests
{
    private const decimal QuoteToAud = 1.0m / 98m; // JPY → AUD

    /// <summary>Buys exactly once, on the candle where Count == EntryAtCount.</summary>
    private sealed class SingleEntryStrategy(int entryAtCount, decimal slOffset, decimal tpOffset) : IStrategy
    {
        public string Name => "test-single-entry";

        public Decision Evaluate(IReadOnlyList<Candle> candles, DateTime nowUtc, bool hasOpenPosition, bool isLongPosition)
        {
            if (hasOpenPosition || candles.Count != entryAtCount)
                return new Decision(TradeAction.Hold);

            var price = candles[^1].Close;
            return new Decision(TradeAction.Buy, StopLoss: price - slOffset, TakeProfit: price + tpOffset);
        }
    }

    private static List<BacktestCandle> BuildCandles(int count, int riseFromIndex)
    {
        // Flat at 150.000, then rising 0.01/candle so the TP gets hit.
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var candles = new List<BacktestCandle>(count);
        for (var i = 0; i < count; i++)
        {
            var close = i <= riseFromIndex ? 150.000m : 150.000m + 0.01m * (i - riseFromIndex);
            candles.Add(new BacktestCandle(
                Time: start.AddMinutes(15 * i),
                Open: close, High: close + 0.005m, Low: close - 0.005m, Close: close,
                Volume: 1000, Instrument: "USD_JPY", Timeframe: "M15"));
        }
        return candles;
    }

    private static BacktestEngine BuildEngine(List<BacktestCandle> candles)
    {
        var provider = new Mock<IHistoricalDataProvider>();
        provider
            .Setup(p => p.GetHistoricalDataAsync("USD_JPY", "M15", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(candles);

        // dbContext is only used by Save/Get, never by RunBacktestAsync
        return new BacktestEngine(provider.Object, null!, NullLogger<BacktestEngine>.Instance);
    }

    [Fact]
    public async Task TightStopJpyTrade_IsMarginCapped_AndPnLIsInAccountCurrency()
    {
        // Entry @150.000 with SL 0.05 below: raw risk size = 250 / (0.05/98) = 490,000 units,
        // but marginCap = (10,000 × 0.40) / (150 × (1/30) × (1/98)) = 78,400 → margin cap binds.
        var candles = BuildCandles(400, riseFromIndex: 260);
        var engine  = BuildEngine(candles);

        var request = new BacktestRequest(
            Name: "sizing-test",
            Strategy: new SingleEntryStrategy(entryAtCount: 255, slOffset: 0.05m, tpOffset: 0.10m),
            Instrument: "USD_JPY",
            Timeframe: "M15",
            StartDate: candles[0].Time,
            EndDate: candles[^1].Time,
            InitialBalance: 10_000m,
            RiskPerTrade: 0.025m,
            Commission: 7m,
            SpreadPips: 0.5m,
            Leverage: 30m,
            MarginUtilisationLimit: 0.40m,
            MaxPositionUnits: 1_000_000m,
            QuoteToAccountRate: QuoteToAud);

        var result = await engine.RunBacktestAsync(request);

        var trade = Assert.Single(result.Trades);
        Assert.Equal(78_400m, trade.Units);          // margin cap, not the 490,000 risk size
        Assert.Equal("TakeProfit", trade.ExitReason);

        // P&L in AUD: 78,400 × 0.100 JPY × (1/98)  = 80.00 gross
        // commission: 78,400/100k × 7               =  5.488
        // spread:     78,400 × 0.5 pip × 0.01 × (1/98) = 4.00
        const decimal expectedPnL = 80.00m - 5.488m - 4.00m;
        Assert.Equal(expectedPnL, Math.Round(trade.PnL, 3));
        Assert.Equal(10_000m + expectedPnL, Math.Round(result.FinalBalance, 3));
    }

    [Fact]
    public async Task WideStopTrade_GetsFullRiskSize()
    {
        // SL 0.50 below: raw = 250 / (0.50/98) = 49,000 units < marginCap 78,400 → risk path wins.
        var candles = BuildCandles(400, riseFromIndex: 260);
        var engine  = BuildEngine(candles);

        var request = new BacktestRequest(
            Name: "sizing-test-wide",
            Strategy: new SingleEntryStrategy(entryAtCount: 255, slOffset: 0.50m, tpOffset: 0.10m),
            Instrument: "USD_JPY",
            Timeframe: "M15",
            StartDate: candles[0].Time,
            EndDate: candles[^1].Time,
            InitialBalance: 10_000m,
            RiskPerTrade: 0.025m,
            QuoteToAccountRate: QuoteToAud);

        var result = await engine.RunBacktestAsync(request);

        var trade = Assert.Single(result.Trades);
        Assert.Equal(49_000m, trade.Units);

        // A stop-out on this size would lose 49,000 × 0.50 × (1/98) = 250 AUD = exactly 2.5%.
        Assert.Equal(250m, Math.Round(49_000m * 0.50m * QuoteToAud, 2));
    }
}
