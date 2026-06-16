using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Core.Brokers;

/// <summary>
/// Broker port — the only surface through which the engine talks to a broker.
/// OANDA is one adapter behind this interface; a future broker is a new adapter.
///
/// Canonical instrument format: uppercase BASE_QUOTE with an underscore,
/// e.g. "EUR_USD", "USD_JPY" (OANDA v20 style). All instrument strings crossing
/// this interface use that format; adapters are responsible for mapping to and
/// from their broker's native naming.
/// </summary>
public interface IBrokerClient
{
    /// <summary>Static capabilities of this broker (name, effective leverage).</summary>
    BrokerDescriptor Descriptor { get; }

    Task<TradeResult> PlaceMarketOrderAsync(TradeSignal signal, decimal stopLoss, decimal takeProfit, long units, CancellationToken ct = default);
    Task<TradeResult> ClosePositionAsync(string instrument, CancellationToken ct = default);
    Task<decimal> GetAccountBalanceAsync(CancellationToken ct = default);
    Task<decimal?> GetOpenPositionUnitsAsync(string instrument, CancellationToken ct = default);
    Task<decimal?> GetMidPriceAsync(string instrument, CancellationToken ct = default);
    Task<PriceSnapshot?> GetPriceSnapshotAsync(string instrument, CancellationToken ct = default);
    /// <summary>Returns all instruments with an open position. Empty list on failure.</summary>
    Task<IReadOnlyList<OpenPositionSummary>> GetAllOpenPositionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Account transactions in [from, to]. Not yet consumed by the live path —
    /// part of the port ahead of realised-P&amp;L work; adapters may throw
    /// <see cref="NotImplementedException"/> until then.
    /// </summary>
    Task<IReadOnlyList<BrokerTransaction>> GetTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
