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

    public async Task<SizingBreakdown> CalculateUnitsAsync(
        TradeSignal signal,
        decimal accountBalance,
        CancellationToken ct = default)
    {
        // Signal override (explicitly > 0) takes priority, then the DB row, then config default
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

        var breakdown = new SizingBreakdown
        {
            RiskPercent    = riskPct,
            RiskSource     = riskSource,
            AccountBalance = accountBalance,
            RiskAmount     = riskAmount,
            StopDistance   = stopDistance,
            StopSource     = stopSource,
            Atr            = signal.Atr,
            QuoteToAud     = quoteToAud,
            LossPerUnit    = lossPerUnit
        };

        if (lossPerUnit <= 0)
        {
            _logger.LogError(
                "Sizing aborted for {Instrument}: lossPerUnit={LossPerUnit} (stopDistance={StopDist}, quoteToAud={QuoteToAud})",
                signal.Instrument, lossPerUnit, stopDistance, quoteToAud);
            return breakdown with { Units = 0, CapReason = "aborted" };
        }

        var raw = riskAmount / lossPerUnit;

        // Per-instrument margin cap: no single trade may consume more than the
        // instrument's margin_cap_percent (default Risk:DefaultMarginCapPercent, 28%).
        // Leverage comes from the broker descriptor (OANDA AU 30:1 → marginRate = 1/30).
        // quoteToAud normalises JPY/USD/EUR → AUD; price × marginRate × quoteToAud is
        // the AUD margin consumed per unit.
        var marginCapPct = (dbSettings?.MarginCapPercent ?? _risk.DefaultMarginCapPercent) / 100m;
        var marginRate   = 1.0m / _broker.Descriptor.Leverage;
        var marginPerUnit = signal.Price * marginRate * quoteToAud;
        var marginCap = (signal.Price > 0)
            ? (accountBalance * marginCapPct) / marginPerUnit
            : _risk.MaxPositionUnits;

        // Aggregate margin safety net: margin already committed by open positions plus
        // this trade must stay under Risk:TotalMarginCeilingPercent of the balance.
        // Sessions overlap across instruments, so per-instrument caps alone don't bound
        // total exposure. The new trade shrinks to fit the remaining headroom.
        decimal existingMarginAud = 0m;
        decimal? aggregateCap = null;
        if (signal.Price > 0)
        {
            var openPositions = await _broker.GetAllOpenPositionsAsync(ct) ?? [];
            foreach (var pos in openPositions)
            {
                var posQuoteToAud = pos.Instrument == signal.Instrument
                    ? quoteToAud
                    : await GetQuoteToAudAsync(pos.Instrument, ct);
                existingMarginAud += Math.Abs(pos.Units) * pos.AveragePrice * marginRate * posQuoteToAud;
            }

            var ceilingAud  = accountBalance * (_risk.TotalMarginCeilingPercent / 100m);
            var headroomAud = Math.Max(0m, ceilingAud - existingMarginAud);
            aggregateCap    = headroomAud / marginPerUnit;
        }

        var capped = raw;
        string? capReason = null;
        if (_risk.MaxPositionUnits < capped)
        {
            capped    = _risk.MaxPositionUnits;
            capReason = "max-position-units";
        }
        if (marginCap < capped)
        {
            capped    = marginCap;
            capReason = "margin-cap";
        }
        if (aggregateCap is { } agg && agg < capped)
        {
            capped    = agg;
            capReason = "aggregate-margin-cap";
        }
        var units = (long)Math.Round(capped);

        _logger.LogInformation(
            "Sizing {Instrument} {Direction} | riskSource={RiskSource} riskPct={RiskPct}% riskAmount={RiskAmount:F2} AUD | " +
            "stopSource={StopSource} stopDist={StopDist} quoteToAud={QuoteToAud:F4} lossPerUnit={LossPerUnit:F6} | " +
            "raw={Raw:F0} marginCap={MarginCap:F0} existingMargin={ExistingMargin:F2} AUD aggregateCap={AggregateCap:F0} " +
            "units={Units} capReason={CapReason}",
            signal.Instrument, signal.Direction,
            riskSource, riskPct, riskAmount,
            stopSource, stopDistance, quoteToAud, lossPerUnit,
            raw, marginCap, existingMarginAud, aggregateCap ?? -1m, units, capReason ?? "none");

        return breakdown with
        {
            Units             = units,
            RawUnits          = raw,
            MarginCapUnits    = marginCap,
            ExistingMarginAud = existingMarginAud,
            AggregateCapUnits = aggregateCap,
            CapReason         = capReason
        };
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
