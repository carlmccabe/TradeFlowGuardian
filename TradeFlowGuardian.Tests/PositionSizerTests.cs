using Microsoft.Extensions.Options;
using Moq;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Enums;
using TradeFlowGuardian.Core.Brokers;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using TradeFlowGuardian.Infrastructure.Sizing;
using Xunit;

namespace TradeFlowGuardian.Tests;

public class PositionSizerTests
{
    // ── shared fixture helpers ────────────────────────────────────────────────

    private static PositionSizer BuildSizer(
        Mock<IBrokerClient> oandaMock,
        decimal defaultRiskPct = 1.0m,
        decimal atrStopMultiplier = 2.0m,
        decimal maxPositionUnits = 1_000_000m)
    {
        var risk = Options.Create(new RiskConfig
        {
            DefaultRiskPercent   = defaultRiskPct,
            MaxPositionUnits     = maxPositionUnits,
            AtrStopMultiplier    = atrStopMultiplier,
            AtrTargetMultiplier  = 4.0m,
            MaxDailyDrawdownPercent = 3.0m
        });
        var riskRepo = new Mock<IRiskSettingsRepository>();
        riskRepo.Setup(r => r.GetByInstrumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradeFlowGuardian.Core.Models.RiskSettings?)null);
        oandaMock.SetupGet(c => c.Descriptor).Returns(new BrokerDescriptor("oanda", 30m));
        return new PositionSizer(risk, oandaMock.Object, riskRepo.Object);
    }

    private static Mock<IBrokerClient> OandaWithAudJpy(decimal audJpy)
    {
        var mock = new Mock<IBrokerClient>();
        mock.Setup(c => c.GetMidPriceAsync("AUD_JPY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(audJpy);
        return mock;
    }

    // ── USD_JPY concrete trace ────────────────────────────────────────────────

    /// <summary>
    /// Concrete trace from the audit:
    ///   Price=149.50, StopLoss=148.80, ATR=1.0 (must be ignored when SL present)
    ///   AUDJPY=98 → quoteToAud = 1/98
    ///   Balance=10,000 AUD, RiskPercent=1.5%
    ///
    ///   stopDistance  = abs(149.50 - 148.80)     = 0.70
    ///   lossPerUnit   = 0.70 × (1/98)            ≈ 0.007143 AUD
    ///   riskAmount    = 10,000 × 1.5%            = 150 AUD
    ///   raw           = 150 / 0.007143           ≈ 20,999.7  → rounds to 21,000
    ///
    ///   marginCap     = (10,000 × 0.28) / (149.50 × (1/30) × (1/98))
    ///                 = 2,800 / (149.50 / 2940)
    ///                 = 2,800 / 0.050850...
    ///                 ≈ 55,059 units (cap not binding)
    ///
    ///   Expected result: 21,000 units (risk path wins)
    /// </summary>
    [Fact]
    public async Task UsdJpy_PreCalculatedSL_UsesActualStopDistance_NotAtr()
    {
        const decimal price      = 149.50m;
        const decimal stopLoss   = 148.80m;
        const decimal atr        = 1.0m;    // wide — would give very different size if used
        const decimal balance    = 10_000m;
        const decimal riskPct    = 1.5m;
        const decimal audJpy     = 98m;

        var oanda  = OandaWithAudJpy(audJpy);
        var sizer  = BuildSizer(oanda);
        var signal = new TradeSignal
        {
            Instrument     = "USD_JPY",
            Direction      = SignalDirection.Long,
            Price          = price,
            StopLoss       = stopLoss,
            Atr            = atr,
            RiskPercent    = riskPct,
            Timestamp      = DateTime.UtcNow,
            IdempotencyKey = "test-usdjpy-1"
        };

        var units = await sizer.CalculateUnitsAsync(signal, balance);

        // stopDistance = 0.70 (from SL, not ATR×2 = 2.0)
        // riskAmount / lossPerUnit = 150 / (0.70/98) ≈ 21,000
        Assert.Equal(21_000L, units);
    }

    [Fact]
    public async Task UsdJpy_PreCalculatedSL_MarginCapIsNotBinding()
    {
        // With the above inputs, marginCap ≈ 55,059 — well above the 21,000 risk units.
        // This test confirms the risk-based size is returned unclipped.
        const decimal price    = 149.50m;
        const decimal stopLoss = 148.80m;
        const decimal balance  = 10_000m;
        const decimal audJpy   = 98m;

        var oanda  = OandaWithAudJpy(audJpy);
        var sizer  = BuildSizer(oanda);
        var signal = new TradeSignal
        {
            Instrument     = "USD_JPY",
            Direction      = SignalDirection.Long,
            Price          = price,
            StopLoss       = stopLoss,
            Atr            = 1.0m,
            RiskPercent    = 1.5m,
            Timestamp      = DateTime.UtcNow,
            IdempotencyKey = "test-usdjpy-margincap"
        };

        var units = await sizer.CalculateUnitsAsync(signal, balance);

        // marginCap ≈ 55,059; risk size ≈ 21,000 — risk path wins
        Assert.True(units < 55_059L,
            $"Expected units ({units}) to be well below margin cap (~55,059)");
        Assert.Equal(21_000L, units);
    }

    // ── ATR fallback path ─────────────────────────────────────────────────────

    [Fact]
    public async Task UsdJpy_NoPreCalculatedSL_FallsBackToAtrMultiplier()
    {
        // StopLoss = 0 → falls back to ATR × AtrStopMultiplier (2.0)
        // stopDistance = 0.5 × 2.0 = 1.0
        // lossPerUnit = 1.0 × (1/98) ≈ 0.010204 AUD
        // riskAmount = 10,000 × 1.0% = 100 AUD
        // raw = 100 / 0.010204 ≈ 9,800 → rounds to 9,800
        const decimal audJpy  = 98m;
        const decimal balance = 10_000m;

        var oanda  = OandaWithAudJpy(audJpy);
        var sizer  = BuildSizer(oanda, defaultRiskPct: 1.0m, atrStopMultiplier: 2.0m);
        var signal = new TradeSignal
        {
            Instrument     = "USD_JPY",
            Direction      = SignalDirection.Long,
            Price          = 149.50m,
            StopLoss       = 0m,        // absent — triggers ATR path
            Atr            = 0.5m,
            RiskPercent    = 0m,        // use default 1%
            Timestamp      = DateTime.UtcNow,
            IdempotencyKey = "test-usdjpy-atr"
        };

        var units = await sizer.CalculateUnitsAsync(signal, balance);

        Assert.Equal(9_800L, units);
    }

    // ── margin cap binding ────────────────────────────────────────────────────

    [Fact]
    public async Task UsdJpy_MarginCapBinds_WhenRiskSizeExceedsIt()
    {
        // Force a huge risk% so raw units exceed the margin cap.
        // riskAmount = 10,000 × 50% = 5,000 AUD
        // stopDistance = abs(149.50 - 149.45) = 0.05
        // lossPerUnit = 0.05 / 98 ≈ 0.000510 AUD
        // raw ≈ 9,800,000 units — far above both MaxPositionUnits and marginCap
        //
        // marginCap = (10,000 × 0.28) / (149.50 × (1/30) × (1/98)) ≈ 55,059
        // MaxPositionUnits = 1,000,000
        // → margin cap of ~55,059 wins
        const decimal audJpy  = 98m;
        const decimal balance = 10_000m;

        var oanda  = OandaWithAudJpy(audJpy);
        var sizer  = BuildSizer(oanda, defaultRiskPct: 50.0m);
        var signal = new TradeSignal
        {
            Instrument     = "USD_JPY",
            Direction      = SignalDirection.Long,
            Price          = 149.50m,
            StopLoss       = 149.45m,   // tiny 0.05 distance → enormous raw size
            Atr            = 0.5m,
            RiskPercent    = 50m,
            Timestamp      = DateTime.UtcNow,
            IdempotencyKey = "test-usdjpy-capbinding"
        };

        var units = await sizer.CalculateUnitsAsync(signal, balance);

        // Exact marginCap = 2,800 / (149.50 / 2,940) = 2,800 / 0.050850... ≈ 55,059
        const long expectedMarginCap = 55_059L;
        Assert.InRange(units, expectedMarginCap - 5, expectedMarginCap + 5);
    }

    // ── zero / guard cases ────────────────────────────────────────────────────

    [Fact]
    public async Task ReturnsZero_WhenLossPerUnitIsZero()
    {
        // ATR = 0, StopLoss = 0 → stopDistance = 0 → lossPerUnit = 0 → return 0
        var oanda  = OandaWithAudJpy(98m);
        var sizer  = BuildSizer(oanda);
        var signal = new TradeSignal
        {
            Instrument     = "USD_JPY",
            Direction      = SignalDirection.Long,
            Price          = 149.50m,
            StopLoss       = 0m,
            Atr            = 0m,
            RiskPercent    = 1.5m,
            Timestamp      = DateTime.UtcNow,
            IdempotencyKey = "test-usdjpy-zero"
        };

        var units = await sizer.CalculateUnitsAsync(signal, 10_000m);

        Assert.Equal(0L, units);
    }
}
