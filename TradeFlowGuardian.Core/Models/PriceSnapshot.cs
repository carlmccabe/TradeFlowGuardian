namespace TradeFlowGuardian.Core.Models;

public record PriceSnapshot(
    string Instrument,
    decimal Bid,
    decimal Ask,
    decimal Mid,
    decimal Spread,
    DateTimeOffset FetchedAt
);
