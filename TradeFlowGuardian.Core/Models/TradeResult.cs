namespace TradeFlowGuardian.Core.Models;

public class TradeResult
{
    public bool Success { get; init; }
    public string? OrderId { get; init; }
    public string? Message { get; init; }
    public decimal? FillPrice { get; init; }
    public long? Units { get; init; }
    public decimal? StopLoss { get; init; }
    public decimal? TakeProfit { get; init; }
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;

    public static TradeResult Succeeded(string orderId, decimal fillPrice, long units, decimal sl, decimal tp) =>
        new() { Success = true, OrderId = orderId, FillPrice = fillPrice, Units = units, StopLoss = sl, TakeProfit = tp };

    public static TradeResult Failed(string reason) =>
        new() { Success = false, Message = reason };
}
