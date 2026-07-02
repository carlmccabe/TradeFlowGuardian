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
    decimal Commission = 7m, // account currency per 100k lot
    decimal SpreadPips = 0.5m,
    // Broker margin model — defaults mirror the live system (OANDA AU 30:1,
    // Risk:MarginUtilisationLimit, Risk:MaxPositionUnits) so simulated sizes
    // match what the live PositionSizer would actually submit.
    decimal Leverage = 30m,
    decimal MarginUtilisationLimit = 0.40m,
    decimal MaxPositionUnits = 1_000_000m,
    // Quote-currency → account-currency conversion (e.g. JPY→AUD ≈ 1/98).
    // Null = use a conservative static rate for the instrument's quote currency.
    decimal? QuoteToAccountRate = null
);

public record CompareStrategiesRequest(
    List<IStrategy> Strategies,
    string Instrument,
    string Timeframe, 
    DateTime StartDate,
    DateTime EndDate,
    decimal InitialBalance = 10000m
);