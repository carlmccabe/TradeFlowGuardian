using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeFlowGuardian.Core.Brokers;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

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

        var lossPerUnit = stopDistance * quoteToAud;

        if (lossPerUnit <= 0)
        {
            _logger.LogError(
                "Sizing aborted for {Instrument}: lossPerUnit={LossPerUnit} (stopDistance={StopDist}, quoteToAud={QuoteToAud})",
                signal.Instrument, lossPerUnit, stopDistance, quoteToAud);
            return 0;
        }

        var raw = riskAmount / lossPerUnit;

        // Margin cap: no single trade may consume more than 28% of account margin.
        // Leverage comes from the broker descriptor (OANDA AU 30:1 → marginRate = 1/30).
        // quoteToAud normalises JPY/USD/EUR → AUD.
        const decimal marginUtilisationLimit = 0.28m;
        var marginRate = 1.0m / _broker.Descriptor.Leverage;
        var marginCap = (signal.Price > 0)
            ? (accountBalance * marginUtilisationLimit) / (signal.Price * marginRate * quoteToAud)
            : _risk.MaxPositionUnits;

        var capped = Math.Min(raw, Math.Min(_risk.MaxPositionUnits, marginCap));
        var units  = (long)Math.Round(capped);

        _logger.LogInformation(
            "Sizing {Instrument} {Direction} | riskSource={RiskSource} riskPct={RiskPct}% riskAmount={RiskAmount:F2} AUD | " +
            "stopSource={StopSource} stopDist={StopDist} quoteToAud={QuoteToAud:F4} lossPerUnit={LossPerUnit:F6} | " +
            "raw={Raw:F0} marginCap={MarginCap:F0} units={Units}",
            signal.Instrument, signal.Direction,
            riskSource, riskPct, riskAmount,
            stopSource, stopDistance, quoteToAud, lossPerUnit,
            raw, marginCap, units);

        return units;
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
