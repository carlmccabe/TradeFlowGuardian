using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Core.Interfaces;

public interface ISignalQueue
{
    Task EnqueueAsync(TradeSignal signal, CancellationToken ct = default);
    Task<TradeSignal?> DequeueAsync(CancellationToken ct = default);
}

public interface ISignalFilter
{
    Task<FilterResult> EvaluateAsync(TradeSignal signal, CancellationToken ct = default);
}

public interface IOandaClient
{
    Task<TradeResult> PlaceMarketOrderAsync(TradeSignal signal, decimal stopLoss, decimal takeProfit, long units, CancellationToken ct = default);
    Task<TradeResult> ClosePositionAsync(string instrument, CancellationToken ct = default);
    Task<decimal> GetAccountBalanceAsync(CancellationToken ct = default);
    Task<decimal?> GetOpenPositionUnitsAsync(string instrument, CancellationToken ct = default);
    Task<decimal?> GetMidPriceAsync(string instrument, CancellationToken ct = default);
}

public interface IPositionSizer
{
    Task<long> CalculateUnitsAsync(TradeSignal signal, decimal accountBalance, CancellationToken ct = default);
}
