using Microsoft.Extensions.Logging.Abstractions;
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
        decimal maxPositionUnits = 1_000_000m,
        RiskSettings? dbSettings = null,
        decimal totalMarginCeilingPct = 75.0m)
    {
        var risk = Options.Create(new RiskConfig
        {
            DefaultRiskPercent   = defaultRiskPct,
            MaxPositionUnits     = maxPositionUnits,
            AtrStopMultiplier    = atrStopMultiplier,
            AtrTargetMultiplier  = 4.0m,
            MaxDailyDrawdownPercent = 3.0m,
            TotalMarginCeilingPercent = totalMarginCeilingPct
        });
        var riskRepo = new Mock<IRiskSettingsRepository>();
        riskRepo.Setup(r => r.GetByInstrumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbSettings);
        oandaMock.SetupGet(c => c.Descriptor).Returns(new BrokerDescriptor("oanda", 30m));
        return new PositionSizer(risk, oandaMock.Object, riskRepo.Object, NullLogger<PositionSizer>.Instance);
    }

    private static Mock<IBrokerClient> OandaWithAudJpy(decimal audJpy)
    {
        var mock = new Mock<IBrokerClient>();
        mock.Setup(c => c.GetMidPriceAsync("AUD_JPY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(audJpy);
        mock.Setup(c => c.GetAllOpenPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
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

        var units = (await sizer.CalculateUnitsAsync(signal, balance)).Units;

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

        var units = (await sizer.CalculateUnitsAsync(signal, balance)).Units;

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

        var units = (await sizer.CalculateUnitsAsync(signal, balance)).Units;

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

        var units = (await sizer.CalculateUnitsAsync(signal, balance)).Units;

        // Exact marginCap = 2,800 / (149.50 / 2,940) = 2,800 / 0.050850... ≈ 55,059
        const long expectedMarginCap = 55_059L;
        Assert.InRange(units, expectedMarginCap - 5, expectedMarginCap + 5);
    }

    // ── per-instrument margin cap (USD_JPY 50% override) ─────────────────────

    private static RiskSettings UsdJpyDbSettings(decimal? marginCapPct) => new()
    {
        Instrument       = "USD_JPY",
        RiskPercent      = 2.5m,
        MarginCapPercent = marginCapPct,
        IsActive         = true
    };

    /// <summary>
    /// USD_JPY at its locked live risk (2.5% from the DB) with the 50% margin cap
    /// override, across the ATR range. Spot 149.50, AUDJPY 98, SL = ATR × 2.6,
    /// balance 10,000 AUD (binding behaviour is balance-invariant: risk amount and
    /// margin ceiling both scale linearly with balance).
    ///
    ///   raw           = 250 / (ATR × 2.6 / 98)
    ///   marginPerUnit = 149.50 × (1/30) × (1/98) ≈ 0.0508503 AUD
    ///   50% cap       = 5,000 / 0.0508503 ≈ 98,328 units
    ///
    ///   ATR 0.06 → raw 157,051, margin need 79.9% → clipped to 98,328 (margin-cap)
    ///   ATR 0.10 → raw  94,231, margin need 47.9% → full risk size
    ///   ATR 0.13 → raw  72,485, margin need 36.9% → full risk size
    ///   ATR 0.17 → raw  55,430, margin need 28.2% → full risk size
    ///                            (would have been clipped by the old flat 28% cap)
    /// </summary>
    [Theory]
    [InlineData("0.06", 98_328L, "margin-cap")]
    [InlineData("0.10", 94_231L, null)]
    [InlineData("0.13", 72_485L, null)]
    [InlineData("0.17", 55_430L, null)]
    public async Task UsdJpy_MarginCap50_SizesFullRisk_WhenMarginNeedAtMostHalf(
        string atrStr, long expectedUnits, string? expectedCapReason)
    {
        var atr    = decimal.Parse(atrStr);
        var oanda  = OandaWithAudJpy(98m);
        var sizer  = BuildSizer(oanda, atrStopMultiplier: 2.6m, dbSettings: UsdJpyDbSettings(50m));
        var signal = new TradeSignal
        {
            Instrument     = "USD_JPY",
            Direction      = SignalDirection.Long,
            Price          = 149.50m,
            StopLoss       = 0m,        // ATR path — stop = ATR × 2.6
            Atr            = atr,
            RiskPercent    = 0m,        // no override — 2.5% comes from the DB row
            Timestamp      = DateTime.UtcNow,
            IdempotencyKey = $"test-usdjpy-cap50-{atrStr}"
        };

        var b = await sizer.CalculateUnitsAsync(signal, 10_000m);

        Assert.Equal("db", b.RiskSource);
        Assert.Equal(2.5m, b.RiskPercent);
        Assert.Equal(250m, b.RiskAmount);                       // 10,000 × 2.5%
        Assert.Equal(expectedUnits, b.Units);
        Assert.Equal(expectedCapReason, b.CapReason);
    }

    [Fact]
    public async Task UsdJpy_NoDbOverride_FallsBackToDefault28Cap()
    {
        // Same ATR 0.17 trade but with no margin_cap_percent in the DB row:
        // the config default (28%) applies and clips 55,430 → 55,064.
        // This pins the exact regression the 50% override exists to fix.
        var oanda  = OandaWithAudJpy(98m);
        var sizer  = BuildSizer(oanda, atrStopMultiplier: 2.6m, dbSettings: UsdJpyDbSettings(null));
        var signal = new TradeSignal
        {
            Instrument     = "USD_JPY",
            Direction      = SignalDirection.Long,
            Price          = 149.50m,
            StopLoss       = 0m,
            Atr            = 0.17m,
            RiskPercent    = 0m,
            Timestamp      = DateTime.UtcNow,
            IdempotencyKey = "test-usdjpy-default28"
        };

        var b = await sizer.CalculateUnitsAsync(signal, 10_000m);

        // 28% cap = 2,800 / 0.0508503 ≈ 55,064 < raw 55,430
        Assert.Equal(55_064L, b.Units);
        Assert.Equal("margin-cap", b.CapReason);
    }

    // ── aggregate margin safety net ───────────────────────────────────────────

    [Fact]
    public async Task AggregateMarginCap_ShrinksNewTradeToFitHeadroom()
    {
        // Open GBP_USD position: 60,000 units @ 1.27, AUDUSD 0.63 →
        //   existing margin = 60,000 × 1.27 × (1/30) × (1/0.63) ≈ 4,031.75 AUD
        // Ceiling 75% of 10,000 = 7,500 → headroom ≈ 3,468.25 AUD
        // New USD_JPY trade at ATR 0.06: raw 157,051, instrument 50% cap 98,328,
        // but aggregate headroom only fits 3,468.25 / 0.0508503 ≈ 68,205 units.
        var oanda = OandaWithAudJpy(98m);
        oanda.Setup(c => c.GetMidPriceAsync("AUD_USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.63m);
        oanda.Setup(c => c.GetAllOpenPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OpenPositionSummary("GBP_USD", 60_000m, 0m, 1.27m)]);

        var sizer  = BuildSizer(oanda, atrStopMultiplier: 2.6m, dbSettings: UsdJpyDbSettings(50m));
        var signal = new TradeSignal
        {
            Instrument     = "USD_JPY",
            Direction      = SignalDirection.Long,
            Price          = 149.50m,
            StopLoss       = 0m,
            Atr            = 0.06m,
            RiskPercent    = 0m,
            Timestamp      = DateTime.UtcNow,
            IdempotencyKey = "test-usdjpy-aggcap"
        };

        var b = await sizer.CalculateUnitsAsync(signal, 10_000m);

        Assert.Equal(68_205L, b.Units);
        Assert.Equal("aggregate-margin-cap", b.CapReason);
        Assert.Equal(4_031.75m, Math.Round(b.ExistingMarginAud, 2));
        Assert.NotNull(b.AggregateCapUnits);
        Assert.Equal(68_205L, (long)Math.Round(b.AggregateCapUnits!.Value));
    }

    [Fact]
    public async Task AggregateMarginCap_ZeroHeadroom_SizesToZero()
    {
        // Existing positions already exceed the 75% ceiling:
        //   120,000 × 1.27 × (1/30) × (1/0.63) ≈ 8,063.49 AUD > 7,500 ceiling
        // → headroom clamps to 0, new trade sizes to 0 units (handler aborts on 0).
        var oanda = OandaWithAudJpy(98m);
        oanda.Setup(c => c.GetMidPriceAsync("AUD_USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.63m);
        oanda.Setup(c => c.GetAllOpenPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OpenPositionSummary("GBP_USD", 120_000m, 0m, 1.27m)]);

        var sizer  = BuildSizer(oanda, atrStopMultiplier: 2.6m, dbSettings: UsdJpyDbSettings(50m));
        var signal = new TradeSignal
        {
            Instrument     = "USD_JPY",
            Direction      = SignalDirection.Long,
            Price          = 149.50m,
            StopLoss       = 0m,
            Atr            = 0.10m,
            RiskPercent    = 0m,
            Timestamp      = DateTime.UtcNow,
            IdempotencyKey = "test-usdjpy-aggzero"
        };

        var b = await sizer.CalculateUnitsAsync(signal, 10_000m);

        Assert.Equal(0L, b.Units);
        Assert.Equal("aggregate-margin-cap", b.CapReason);
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

        var units = (await sizer.CalculateUnitsAsync(signal, 10_000m)).Units;

        Assert.Equal(0L, units);
    }

    // ── sizing breakdown audit trail ──────────────────────────────────────────

    [Fact]
    public async Task Breakdown_ExposesFormulaComponents_AndCapReason()
    {
        // Same inputs as UsdJpy_MarginCapBinds_WhenRiskSizeExceedsIt — the margin cap
        // binds, so the breakdown must say so and carry every formula component.
        const decimal audJpy  = 98m;
        const decimal balance = 10_000m;

        var oanda  = OandaWithAudJpy(audJpy);
        var sizer  = BuildSizer(oanda, defaultRiskPct: 50.0m);
        var signal = new TradeSignal
        {
            Instrument     = "USD_JPY",
            Direction      = SignalDirection.Long,
            Price          = 149.50m,
            StopLoss       = 149.45m,
            Atr            = 0.5m,
            RiskPercent    = 50m,
            Timestamp      = DateTime.UtcNow,
            IdempotencyKey = "test-usdjpy-breakdown"
        };

        var b = await sizer.CalculateUnitsAsync(signal, balance);

        Assert.Equal(50m, b.RiskPercent);
        Assert.Equal("signal-override", b.RiskSource);
        Assert.Equal(balance, b.AccountBalance);
        Assert.Equal(5_000m, b.RiskAmount);                     // 10,000 × 50%
        Assert.Equal(0.05m, b.StopDistance);                    // |149.50 − 149.45|
        Assert.Equal("signal-sl", b.StopSource);
        Assert.Equal(1m / 98m, b.QuoteToAud, precision: 8);
        Assert.Equal(0.05m / 98m, b.LossPerUnit, precision: 8);
        Assert.True(b.RawUnits > b.MarginCapUnits, "raw size should exceed the margin cap");
        Assert.Equal("margin-cap", b.CapReason);
        Assert.Equal(b.Units, (long)Math.Round(b.MarginCapUnits));
    }

    [Fact]
    public async Task Breakdown_NoCapReason_WhenRiskSizeWins()
    {
        var oanda  = OandaWithAudJpy(98m);
        var sizer  = BuildSizer(oanda);
        var signal = new TradeSignal
        {
            Instrument     = "USD_JPY",
            Direction      = SignalDirection.Long,
            Price          = 149.50m,
            StopLoss       = 148.80m,
            Atr            = 1.0m,
            RiskPercent    = 1.5m,
            Timestamp      = DateTime.UtcNow,
            IdempotencyKey = "test-usdjpy-nocap"
        };

        var b = await sizer.CalculateUnitsAsync(signal, 10_000m);

        Assert.Null(b.CapReason);
        Assert.Equal(21_000L, b.Units);
        Assert.Equal(150m, b.RiskAmount);                       // 10,000 × 1.5%
    }
}
