namespace TradeFlowGuardian.Backtesting.Models;

// Historical data models
public record BacktestCandle(DateTime Time, decimal Open, decimal High, decimal Low, decimal Close, long Volume, string Instrument = "", string Timeframe = "");