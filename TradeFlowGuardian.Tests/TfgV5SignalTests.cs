using TradeFlowGuardian.Backtesting.Strategies;
using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Signals.Tfg;
using Xunit;
using DomainTradeAction = TradeFlowGuardian.Domain.Entities.TradeAction;

namespace TradeFlowGuardian.Tests;

/// <summary>
/// Verifies the C# port of the TFG v5 Pine entry logic: gate behavior, ATR-based
/// SL/TP levels, session filtering in the factory preset, and multiplier overrides.
/// </summary>
public class TfgV5SignalTests
{
    // ── fixture: flat → shallow dip → sharp pop, engineered to produce a golden cross ──

    /// <summary>
    /// 240 flat bars @150.000, 12 bars drifting −0.01, 8 bars popping +0.06.
    /// The pop pulls SMA(9) up through SMA(25) with price above the trend EMA,
    /// RSI &gt; 50, ATR rising, and EMA distance inside the 5–69 pip band.
    /// </summary>
    private static List<Candle> BuildCrossSeries()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var candles = new List<Candle>();
        var close = 150.000m;

        void Add(decimal c)
        {
            candles.Add(new Candle
            {
                Time = start.AddMinutes(15 * candles.Count),
                Open = c, High = c + 0.005m, Low = c - 0.005m, Close = c,
                Volume = 1000, Instrument = "USD_JPY", Granularity = "M15"
            });
        }

        for (var i = 0; i < 240; i++) Add(close);
        for (var i = 0; i < 12; i++) { close -= 0.01m; Add(close); }
        for (var i = 0; i < 8; i++) { close += 0.06m; Add(close); }
        return candles;
    }

    private sealed class TestContext(IReadOnlyList<Candle> candles) : IMarketContext
    {
        public DateTime TimestampUtc => candles[^1].Time;
        public string Instrument => "USD_JPY";
        public decimal AccountBalance => 10_000m;
        public decimal AvailableMargin => 10_000m;
        public Position? OpenPosition => null;
        public IReadOnlyList<Candle> Candles => candles;
        public IReadOnlyDictionary<string, IIndicatorResult> Indicators { get; } = new Dictionary<string, IIndicatorResult>();
        public string CorrelationId => "test";
        public IReadOnlyDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();
    }

    /// <summary>Scans the series bar-by-bar and returns (index, result) for every firing bar.</summary>
    private static List<(int Index, SignalResult Result)> ScanFires(TfgV5Signal signal, List<Candle> candles)
    {
        var fires = new List<(int, SignalResult)>();
        for (var i = 200; i < candles.Count; i++)
        {
            var result = signal.Generate(new TestContext(candles.Take(i + 1).ToList()));
            if (result.Direction != SignalDirection.Neutral)
                fires.Add((i, result));
        }
        return fires;
    }

    [Fact]
    public void GoldenCross_FiresLong_WithAllPineGatesSatisfied()
    {
        var candles = BuildCrossSeries();
        var signal  = new TfgV5Signal("test");

        var fires = ScanFires(signal, candles);

        Assert.NotEmpty(fires);
        foreach (var (index, result) in fires)
        {
            Assert.Equal(SignalDirection.Long, result.Direction);

            var close = candles[index].Close;
            var diag  = result.Diagnostics;
            var atr   = (decimal)diag["Atr"];

            // Every Pine gate must hold on a firing bar
            Assert.True((decimal)diag["Ema"] < close, "close must be above trend EMA");
            Assert.True((decimal)diag["Rsi"] > 50m, "RSI must be above 50");
            Assert.True((bool)diag["AtrRising"], "ATR must be rising");
            Assert.InRange((decimal)diag["EmaDistPips"], 5m, 69m);

            // SL/TP mirror Pine: close ∓ mult × ATR
            Assert.Equal(close - 2.6m * atr, result.SuggestedStopLoss);
            Assert.Equal(close + 5.3m * atr, result.SuggestedTakeProfit);
        }
    }

    [Fact]
    public void FlatMarket_NeverFires()
    {
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var candles = Enumerable.Range(0, 260).Select(i => new Candle
        {
            Time = start.AddMinutes(15 * i),
            Open = 150m, High = 150.005m, Low = 149.995m, Close = 150m,
            Volume = 1000, Instrument = "USD_JPY", Granularity = "M15"
        }).ToList();

        Assert.Empty(ScanFires(new TfgV5Signal("test"), candles));
    }

    [Fact]
    public void InsufficientData_ReturnsNeutral()
    {
        var candles = BuildCrossSeries().Take(100).ToList();
        var result  = new TfgV5Signal("test").Generate(new TestContext(candles));

        Assert.Equal(SignalDirection.Neutral, result.Direction);
        Assert.StartsWith("Insufficient data", result.Reason);
    }

    [Fact]
    public void SlTpMultiplierOverrides_ScaleTheLevels()
    {
        var candles = BuildCrossSeries();
        var fires26 = ScanFires(new TfgV5Signal("test", slMult: 2.6m, tpMult: 5.3m), candles);
        var fires40 = ScanFires(new TfgV5Signal("test", slMult: 4.0m, tpMult: 8.0m), candles);

        Assert.NotEmpty(fires26);
        Assert.Equal(fires26.Select(f => f.Index), fires40.Select(f => f.Index)); // gates unchanged

        var (index, r26) = fires26[0];
        var r40   = fires40[0].Result;
        var close = candles[index].Close;
        var atr   = (decimal)r26.Diagnostics["Atr"];

        Assert.Equal(close - 4.0m * atr, r40.SuggestedStopLoss);
        Assert.Equal(close + 8.0m * atr, r40.SuggestedTakeProfit);
    }

    // ── factory preset: full pipeline including the session filter ────────────

    [Fact]
    public void Preset_FiresInSession_AndIsBlockedOutOfSession()
    {
        var candles = BuildCrossSeries();
        var firingIndex = ScanFires(new TfgV5Signal("test"), candles)[0].Index;
        var window = candles.Take(firingIndex + 1).ToList();

        var strategy = StrategyFactory.Create("tfg_usdjpy_v5");

        // Pine session: 00–09 + 11–12 UTC
        var inSession    = new DateTime(2026, 1, 5, 3, 0, 0, DateTimeKind.Utc);
        var lateWindow   = new DateTime(2026, 1, 5, 11, 30, 0, DateTimeKind.Utc);
        var outOfSession = new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(DomainTradeAction.Buy,  strategy.Evaluate(window, inSession, false, false).Action);
        Assert.Equal(DomainTradeAction.Buy,  strategy.Evaluate(window, lateWindow, false, false).Action);
        Assert.Equal(DomainTradeAction.Hold, strategy.Evaluate(window, outOfSession, false, false).Action);
    }

    [Fact]
    public void Preset_PassesMultiplierOverridesThrough()
    {
        var candles = BuildCrossSeries();
        var firingIndex = ScanFires(new TfgV5Signal("test"), candles)[0].Index;
        var window = candles.Take(firingIndex + 1).ToList();
        var nowUtc = new DateTime(2026, 1, 5, 3, 0, 0, DateTimeKind.Utc);

        var narrow = StrategyFactory.Create("tfg_usdjpy_v5").Evaluate(window, nowUtc, false, false);
        var wide   = StrategyFactory.Create("tfg_usdjpy_v5", slMultiplier: 5.2m, tpMultiplier: 10.6m)
            .Evaluate(window, nowUtc, false, false);

        Assert.NotNull(narrow.StopLoss);
        Assert.NotNull(wide.StopLoss);

        var close = window[^1].Close;
        // Doubling both multipliers doubles the distances (rounded: decimal mult. ordering
        // differs in the last of 28 digits)
        Assert.Equal(2m * (close - narrow.StopLoss!.Value), close - wide.StopLoss!.Value, 10);
        Assert.Equal(2m * (narrow.TakeProfit!.Value - close), wide.TakeProfit!.Value - close, 10);
    }
}
