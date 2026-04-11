namespace TradeFlowGuardian.Core.Models;

/// <summary>
/// Summary of a single open position, returned from OANDA's /openPositions endpoint.
/// </summary>
public record OpenPositionSummary(
    string Instrument,
    /// <summary>Net units. Positive = long, negative = short.</summary>
    decimal Units,
    decimal UnrealizedPL,
    decimal AveragePrice
);
