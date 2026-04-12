namespace TradeFlowGuardian.Domain.Entities;

public record EquityPoint(DateTime Timestamp, decimal Balance, decimal Equity, decimal DrawdownPercent);

