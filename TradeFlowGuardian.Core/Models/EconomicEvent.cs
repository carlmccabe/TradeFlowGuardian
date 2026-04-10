namespace TradeFlowGuardian.Core.Models;

/// <summary>Impact ranking — ordered so numeric comparison works (High > Medium > Low).</summary>
public enum ImpactLevel { Unknown = 0, Low = 1, Medium = 2, High = 3 }

/// <summary>A single scheduled economic release from the ForexFactory calendar.</summary>
public record EconomicEvent(
    string Currency,
    string Title,
    DateTimeOffset ScheduledAt,
    ImpactLevel Impact);
