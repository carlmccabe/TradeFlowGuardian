using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeFlowGuardian.Core.Brokers;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using TradeFlowGuardian.Core.Sizing;

namespace TradeFlowGuardian.Infrastructure.Sizing;

/// <summary>
/// Mirrors the Pine Script Section 5 position sizing formula.
/// Units = RiskAmount (AUD) / (StopDistance × QuoteToAUD)
/// </summary>
public class PositionSizer : IPositionSizer
{
    private readonly RiskConfig _risk;
    private readonly IBrokerClient _broker;
    private readonly IRiskSettingsRepository _riskRepo;
    private readonly ILogger<PositionSizer> _logger;

    public PositionSizer(IOptions<RiskConfig> risk, IBrokerClient broker, IRiskSettingsRepository riskRepo, ILogger<PositionSizer> logger)
    {
        _risk     = risk.Value;
        _broker   = broker;
        _riskRepo = riskRepo;
        _logger   = logger;
    }

    public async Task<long> CalculateUnitsAsync(
        TradeSignal signal,
        decimal accountBalance,
        CancellationToken ct = default)
    {
        // DB lookup takes priority; signal override applies only when explicitly > 0
        var dbSettings = await _riskRepo.GetByInstrumentAsync(signal.Instrument, ct);

        string riskSource;
        decimal riskPct;
        if (signal.RiskPercent > 0)
        {
            riskPct    = signal.RiskPercent;
            riskSource = "signal-override";
        }
        else if (dbSettings?.RiskPercent is { } dbPct)
        {
            riskPct    = dbPct;
            riskSource = "db";
        }
        else
        {
            riskPct    = _risk.DefaultRiskPercent;
            riskSource = dbSettings is null ? "config-default(db-null)" : "config-default";
        }

        var riskAmount = accountBalance * (riskPct / 100m);

        // Prefer actual SL distance from the webhook; fall back to ATR × multiplier
        bool usingSl = signal.StopLoss > 0 && signal.Price > 0;
        var stopDistance = usingSl
            ? Math.Abs(signal.Price - signal.StopLoss)
            : signal.Atr * _risk.AtrStopMultiplier;
        var stopSource = usingSl ? "signal-sl" : $"atr×{_risk.AtrStopMultiplier}";

        var quoteToAud = await GetQuoteToAudAsync(signal.Instrument, ct);

        // Margin cap: no single trade may consume more than Risk:MarginUtilisationLimit
        // of account margin. Leverage comes from the broker descriptor
        // (OANDA AU 30:1 → marginRate = 1/30). quoteToAud normalises JPY/USD/EUR → AUD.
        var size = PositionSizeCalculator.Calculate(
            accountBalance, riskAmount, stopDistance, quoteToAud,
            signal.Price, _broker.Descriptor.Leverage,
            _risk.MarginUtilisationLimit, _risk.MaxPositionUnits);

        if (size.Units <= 0)
        {
            _logger.LogError(
                "Sizing aborted for {Instrument}: units=0 (stopDistance={StopDist}, quoteToAud={QuoteToAud}, riskAmount={RiskAmount})",
                signal.Instrument, stopDistance, quoteToAud, riskAmount);
            return 0;
        }

        if (size.BindingCap != PositionSizeCap.None)
        {
            _logger.LogWarning(
                "Position size capped for {Instrument}: risk formula wants {Raw:F0} units " +
                "but {CapSource} allows only {Capped}. Effective risk ≈ {EffectiveRiskPct:F2}% " +
                "instead of the configured {RiskPct}%.",
                signal.Instrument, size.RawUnits,
                size.BindingCap == PositionSizeCap.MarginLimit ? $"margin limit ({_risk.MarginUtilisationLimit:P0})" : "MaxPositionUnits",
                size.Units, size.EffectiveRiskPercent(accountBalance), riskPct);
        }

        _logger.LogInformation(
            "Sizing {Instrument} {Direction} | riskSource={RiskSource} riskPct={RiskPct}% riskAmount={RiskAmount:F2} AUD | " +
            "stopSource={StopSource} stopDist={StopDist} quoteToAud={QuoteToAud:F4} lossPerUnit={LossPerUnit:F6} | " +
            "raw={Raw:F0} marginCap={MarginCap:F0} units={Units}",
            signal.Instrument, signal.Direction,
            riskSource, riskPct, riskAmount,
            stopSource, stopDistance, quoteToAud, size.LossPerUnit,
            size.RawUnits, size.MarginCapUnits, size.Units);

        return size.Units;
    }

    /// <summary>
    /// Fetches live conversion rate from quote currency to AUD.
    /// Matches the Pine getQuoteToAUD() switch logic exactly.
    /// </summary>
    private async Task<decimal> GetQuoteToAudAsync(string instrument, CancellationToken ct)
    {
        // instrument format: "USD_JPY", "EUR_USD", "GBP_USD"
        var quoteCurrency = instrument.Split('_').LastOrDefault() ?? "USD";

        return quoteCurrency switch
        {
            "AUD" => 1.0m,
            "USD" => await GetRateAsync("AUD_USD", invert: true, ct),   // 1 / AUDUSD
            "JPY" => await GetRateAsync("AUD_JPY", invert: true, ct),   // 1 / AUDJPY
            "CAD" => await GetRateAsync("AUD_CAD", invert: true, ct),
            "CHF" => await GetRateAsync("AUD_CHF", invert: true, ct),
            "NZD" => await GetRateAsync("AUD_NZD", invert: true, ct),
            "GBP" => await GetRateAsync("GBP_AUD", invert: false, ct),  // GBPAUD directly
            "EUR" => await GetRateAsync("EUR_AUD", invert: false, ct),
            _ => 1.0m
        };
    }

    /// <summary>
    /// Fetches live mid price for an OANDA instrument via the pricing snapshot endpoint.
    /// Falls back to conservative hardcoded rates if the API call fails.
    /// </summary>
    private async Task<decimal> GetRateAsync(string instrument, bool invert, CancellationToken ct)
    {
        var live = await _broker.GetMidPriceAsync(instrument, ct);
        if (live is > 0)
            return invert ? (1.0m / live.Value) : live.Value;

        // Conservative fallbacks — used only when pricing endpoint is unreachable
        var fallbacks = new Dictionary<string, decimal>
        {
            ["AUD_USD"] = 0.63m,
            ["AUD_JPY"] = 98m,
            ["AUD_CAD"] = 0.87m,
            ["AUD_CHF"] = 0.57m,
            ["AUD_NZD"] = 1.09m,
            ["GBP_AUD"] = 1.97m,
            ["EUR_AUD"] = 1.72m
        };

        var rate = fallbacks.GetValueOrDefault(instrument, 1.0m);
        return invert ? (1.0m / rate) : rate;
    }
}
