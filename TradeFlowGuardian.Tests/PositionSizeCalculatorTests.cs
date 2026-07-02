using TradeFlowGuardian.Core.Sizing;
using Xunit;

namespace TradeFlowGuardian.Tests;

/// <summary>
/// Pins the shared sizing formula used by BOTH the live PositionSizer and the
/// backtest engine. Numbers mirror the concrete traces in PositionSizerTests so
/// any drift between live and simulated sizing shows up here.
/// </summary>
public class PositionSizeCalculatorTests
{
    // Shared trace inputs: USD_JPY @ 149.50, AUDJPY = 98, balance 10,000 AUD, 30:1
    private const decimal Balance    = 10_000m;
    private const decimal Price      = 149.50m;
    private const decimal QuoteToAud = 1.0m / 98m;
    private const decimal Leverage   = 30m;
    private const decimal MaxUnits   = 1_000_000m;

    [Fact]
    public void RiskPathWins_WhenMarginCapNotBinding()
    {
        // riskAmount = 150 (1.5%), stopDistance 0.70 → raw ≈ 21,000; marginCap ≈ 55,059
        var result = PositionSizeCalculator.Calculate(
            Balance, riskAmount: 150m, stopDistance: 0.70m, QuoteToAud,
            Price, Leverage, marginUtilisationLimit: 0.28m, MaxUnits);

        Assert.Equal(21_000L, result.Units);
        Assert.Equal(PositionSizeCap.None, result.BindingCap);
        Assert.InRange(result.MarginCapUnits, 55_054m, 55_064m);
    }

    [Fact]
    public void MarginCapBinds_OnTightStop()
    {
        // riskAmount = 5,000 (50%), stopDistance 0.05 → raw ≈ 9.8M; marginCap ≈ 55,059
        var result = PositionSizeCalculator.Calculate(
            Balance, riskAmount: 5_000m, stopDistance: 0.05m, QuoteToAud,
            Price, Leverage, marginUtilisationLimit: 0.28m, MaxUnits);

        Assert.InRange(result.Units, 55_054L, 55_064L);
        Assert.Equal(PositionSizeCap.MarginLimit, result.BindingCap);
    }

    [Fact]
    public void MarginCap_ScalesLinearlyWithUtilisationLimit()
    {
        var at28 = PositionSizeCalculator.Calculate(
            Balance, 5_000m, 0.05m, QuoteToAud, Price, Leverage, 0.28m, MaxUnits);
        var at40 = PositionSizeCalculator.Calculate(
            Balance, 5_000m, 0.05m, QuoteToAud, Price, Leverage, 0.40m, MaxUnits);

        // 55,059 × (0.40 / 0.28) ≈ 78,662
        Assert.InRange(at40.Units, 78_657L, 78_667L);
        Assert.Equal(
            Math.Round(at28.MarginCapUnits / 0.28m * 0.40m),
            Math.Round(at40.MarginCapUnits));
    }

    [Fact]
    public void MaxUnitsBinds_WhenLowerThanMarginCap()
    {
        var result = PositionSizeCalculator.Calculate(
            Balance, 5_000m, 0.05m, QuoteToAud, Price, Leverage,
            marginUtilisationLimit: 0.28m, maxPositionUnits: 40_000m);

        Assert.Equal(40_000L, result.Units);
        Assert.Equal(PositionSizeCap.MaxUnits, result.BindingCap);
    }

    [Fact]
    public void EffectiveRiskPercent_ReflectsCappedSize()
    {
        // Wants 50% risk but margin-capped: effective = units × lossPerUnit / balance
        var result = PositionSizeCalculator.Calculate(
            Balance, 5_000m, 0.05m, QuoteToAud, Price, Leverage, 0.28m, MaxUnits);

        var expected = result.Units * result.LossPerUnit / Balance * 100m;
        Assert.Equal(expected, result.EffectiveRiskPercent(Balance));
        Assert.True(result.EffectiveRiskPercent(Balance) < 1m,
            "Margin cap should reduce a 50% risk request to well under 1%");
    }

    [Theory]
    [InlineData(0, 0.05)]  // zero risk amount
    [InlineData(150, 0)]   // zero stop distance
    public void ReturnsZero_OnDegenerateInputs(decimal riskAmount, decimal stopDistance)
    {
        var result = PositionSizeCalculator.Calculate(
            Balance, riskAmount, stopDistance, QuoteToAud, Price, Leverage, 0.28m, MaxUnits);

        Assert.Equal(0L, result.Units);
    }

    [Fact]
    public void ZeroPrice_FallsBackToMaxUnitsAsMarginCap()
    {
        // Price unknown → margin cap can't be computed; MaxPositionUnits is the ceiling
        var result = PositionSizeCalculator.Calculate(
            Balance, 5_000m, 0.05m, QuoteToAud, price: 0m, Leverage, 0.28m, 40_000m);

        Assert.Equal(40_000L, result.Units);
    }
}
