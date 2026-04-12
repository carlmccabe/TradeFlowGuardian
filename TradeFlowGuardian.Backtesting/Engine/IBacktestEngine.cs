using TradeFlowGuardian.Backtesting.Models;
using TradeFlowGuardian.Domain.Entities;

namespace TradeFlowGuardian.Backtesting.Engine;

public interface IBacktestEngine
{
    Task<BacktestResult> RunBacktestAsync(BacktestRequest request, CancellationToken cancellationToken = default);
    Task<List<BacktestResult>> CompareStrategiesAsync(CompareStrategiesRequest request, CancellationToken cancellationToken = default);
    Task<BacktestResult> GetBacktestResultAsync(Guid backtestId);
    Task SaveBacktestResultAsync(BacktestResult result);
}

public record BacktestRequest(
    string Name,
    IStrategy Strategy,
    string Instrument,
    string Timeframe,
    DateTime StartDate,
    DateTime EndDate,
    decimal InitialBalance = 10000m,
    decimal RiskPerTrade = 0.01m,
    decimal Commission = 7m, // USD per 100k lot
    decimal SpreadPips = 0.5m
);

public record CompareStrategiesRequest(
    List<IStrategy> Strategies,
    string Instrument,
    string Timeframe, 
    DateTime StartDate,
    DateTime EndDate,
    decimal InitialBalance = 10000m
);