namespace TradeFlowGuardian.Core.Brokers;

/// <summary>
/// A closed broker trade with realized P&amp;L.
/// Not yet consumed by the live path — included in the port ahead of realised-P&amp;L work.
/// </summary>
public record BrokerTransaction(
    string Id,
    string Type,
    string? Instrument,
    decimal Units,
    decimal Price,              // entry fill price
    decimal ClosePrice,         // average close price (0 if unavailable)
    decimal RealizedPL,
    DateTimeOffset OpenedAt,
    DateTimeOffset Timestamp);  // closeTime

