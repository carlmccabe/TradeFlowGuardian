namespace TradeFlowGuardian.Domain.Entities;

public record PriceTick(string Symbol, decimal Bid, decimal Ask, DateTimeOffset Time);

public record AccountSnapshot(decimal Balance, decimal Equity, decimal UnrealizedPnl, DateTimeOffset Timestamp);

public enum Side { Long, Short }

// public record Position(string Symbol, Side Side, decimal Qty, decimal AvgPrice, decimal UnrealizedPnl);

public record StrategyState(Guid Id, string Name, bool IsRunning, string Symbol, decimal Qty);

public class AccountSummary
{
    public string AccountId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal UnrealizedPL { get; set; }
    public decimal RealizedPL { get; set; }
    public decimal MarginUsed { get; set; }
    public decimal MarginAvailable { get; set; }
    public decimal MarginRate { get; set;}
}

// Contracts
public enum TradeAction { Hold, Buy, Sell, Exit }

public record Decision(TradeAction Action, decimal? StopLoss = null, decimal? TakeProfit = null, string Reason = "");

public interface IStrategy
{
    string Name { get; }
    Decision Evaluate(IReadOnlyList<Candle> m5Candles, DateTime nowUtc, bool hasOpenPosition, bool isLongPosition);
}

public interface IRiskManager
{
    // Returns trade units (OANDA "units") based on risk %, SL distance, and account balance
    Task<long> ComputeUnitsAsync(string instrument, decimal accountBalance, decimal stopDistancePrice, decimal riskPercent);
}

public interface ITradeExecutor
{
    Task<bool> HasOpenPositionAsync(string instrument);
    Task<(bool isLong, decimal avgPrice)> GetOpenPositionAsync(string instrument);
    Task<string> MarketEnterAsync(string instrument, bool isLong, long units, decimal? stopLoss, decimal? takeProfit, string tag);
    Task ClosePositionAsync(string instrument);
}

public interface IClock { DateTime UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
