using TradeFlowGuardian.Backtesting.Models;
using TradeFlowGuardian.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace TradeFlowGuardian.Backtesting.Data;

public interface IHistoricalDataProvider
{
    Task<List<BacktestCandle>> GetHistoricalDataAsync(string instrument, string timeframe, DateTime startDate, DateTime endDate);
    Task<bool> IsDataAvailableAsync(string instrument, string timeframe, DateTime startDate, DateTime endDate);
    Task<DateTime?> GetEarliestDataDateAsync(string instrument, string timeframe);
    Task<DateTime?> GetLatestDataDateAsync(string instrument, string timeframe);
}