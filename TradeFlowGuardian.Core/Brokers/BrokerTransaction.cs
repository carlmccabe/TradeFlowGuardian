namespace TradeFlowGuardian.Core.Brokers;

/// <summary>
/// A broker account transaction (fill, close, financing adjustment, …).
/// Not yet consumed by the live path — included in the port ahead of realised-P&amp;L work.
/// Shape is intentionally minimal and may grow when that feature lands.
/// </summary>
public record BrokerTransaction(
    string Id,
    string Type,
    string? Instrument,
    decimal Units,
    decimal Price,
    decimal RealizedPL,
    DateTimeOffset Timestamp);
