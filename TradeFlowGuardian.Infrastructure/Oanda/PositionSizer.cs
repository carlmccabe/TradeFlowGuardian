using Microsoft.Extensions.Options;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Oanda;

/// <summary>
/// Mirrors the Pine Script Section 5 position sizing formula.
/// Units = RiskAmount (AUD) / (StopDistance × QuoteToAUD)
/// </summary>
public class PositionSizer : IPositionSizer
{
    private readonly RiskConfig _risk;
    private readonly IOandaClient _oanda;

    public PositionSizer(IOptions<RiskConfig> risk, IOandaClient oanda)
    {
        _risk = risk.Value;
        _oanda = oanda;
    }

    public async Task<long> CalculateUnitsAsync(
        TradeSignal signal,
        decimal accountBalance,
        CancellationToken ct = default)
    {
        var riskPct = signal.RiskPercent > 0 ? signal.RiskPercent : _risk.DefaultRiskPercent;
        var riskAmount = accountBalance * (riskPct / 100m);

        var stopDistance = signal.Atr * _risk.AtrStopMultiplier;

        // Fetch live quote-to-AUD conversion rate
        var quoteToAud = await GetQuoteToAudAsync(signal.Instrument, ct);

        var lossPerUnit = stopDistance * quoteToAud;

        if (lossPerUnit <= 0)
            return 0;

        var raw = riskAmount / lossPerUnit;
        var capped = Math.Min(raw, _risk.MaxPositionUnits);
        return (long)Math.Round(capped);
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
        var live = await _oanda.GetMidPriceAsync(instrument, ct);
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
