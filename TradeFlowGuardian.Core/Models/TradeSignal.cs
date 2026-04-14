using TradeFlowGuardian.Core.Enums;

namespace TradeFlowGuardian.Core.Models;

/// <summary>
/// Inbound signal payload from TradingView webhook alert.
/// Configure TV alert message as JSON matching this structure.
/// </summary>
public class TradeSignal
{
    /// <summary>OANDA instrument format e.g. "USD_JPY", "EUR_USD", "GBP_USD"</summary>
    public required string Instrument { get; init; }

    public required SignalDirection Direction { get; init; }

    /// <summary>ATR value at signal time — used to validate filter server-side</summary>
    public decimal Atr { get; init; }

    /// <summary>Price at signal bar close — optional when StopLoss/TakeProfit are supplied directly</summary>
    public decimal Price { get; init; }

    /// <summary>Risk % override — leave 0 to use server config default</summary>
    public decimal RiskPercent { get; init; }

    /// <summary>Pre-calculated stop loss price from Pine Script — skips server-side ATR SL calculation when > 0</summary>
    public decimal StopLoss { get; init; }

    /// <summary>Pre-calculated take profit price from Pine Script — skips server-side ATR TP calculation when > 0</summary>
    public decimal TakeProfit { get; init; }

    /// <summary>Server receive time UTC — always stamped by the API on receipt, TV timestamp is ignored.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Idempotency key — TV alert ID or unique hash to prevent duplicate execution. Optional.</summary>
    public string? IdempotencyKey { get; init; }
}
