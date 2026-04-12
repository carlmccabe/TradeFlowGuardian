// TradeFlowGuardian.Backtesting/Engine/BacktestEngine.cs

using System.Diagnostics;
using System.Text.Json;
using TradeFlowGuardian.Backtesting.Data;
using TradeFlowGuardian.Backtesting.Data.Entities;
using TradeFlowGuardian.Backtesting.Models;
using TradeFlowGuardian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TradeFlowGuardian.Backtesting.Engine;

public class BacktestEngine(
    IHistoricalDataProvider dataProvider,
    BacktestDataContext dbContext,
    ILogger<BacktestEngine> logger)
    : IBacktestEngine
{
    public async Task<BacktestResult> RunBacktestAsync(BacktestRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        logger.LogInformation("🚀 Starting backtest: {Name} on {Instrument} from {Start} to {End}",
            request.Name, request.Instrument, request.StartDate.ToShortDateString(),
            request.EndDate.ToShortDateString());

        // Load historical data
        var candles = await dataProvider.GetHistoricalDataAsync(
            request.Instrument, request.Timeframe, request.StartDate, request.EndDate);

        if (candles.Count < 300)
            throw new InvalidOperationException($"Insufficient data: {candles.Count} candles. Need at least 300.");

        logger.LogInformation("📊 Loaded {Count} candles for backtesting", candles.Count);

        // Initialize backtest state
        var trades = new List<BacktestTrade>();
        var equityCurve = new List<EquityPoint>();
        var balance = request.InitialBalance;
        var equity = request.InitialBalance;
        var openTrade = (BacktestTrade?)null;
        var tradeNumber = 0;
        var maxDrawdown = 0m;
        var peak = request.InitialBalance;

        // Main backtest loop
        for (int i = 200; i < candles.Count; i++) // Skip warmup period
        {
            logger.LogInformation("Backtest loop: {CandleIndex}/{CandleCount} -- Balance: {Balance}", i, candles.Count, balance);
            if (cancellationToken.IsCancellationRequested) break;

            var currentCandles = candles.Take(i + 1).ToList();
            var currentCandle = candles[i];

            // Update equity and drawdown
            var unrealizedPnL = openTrade?.CalculateUnrealizedPnL(currentCandle.Close) ?? 0;
            equity = balance + unrealizedPnL;

            if (equity > peak) peak = equity;
            var currentDrawdown = (peak - equity) / peak;
            if (currentDrawdown > maxDrawdown) maxDrawdown = currentDrawdown;

            equityCurve.Add(new EquityPoint(currentCandle.Time, balance, equity, currentDrawdown));

            // Check for exit conditions first
            if (openTrade != null)
            {
                var exitResult = CheckExitConditions(openTrade, currentCandle);
                if (exitResult.ShouldExit)
                {
                    // Close trade
                    var realizedPnL = openTrade.CalculateRealizedPnL(exitResult.ExitPrice);
                    var commission = CalculateCommission(openTrade.Units, request.Commission);
                    var slippage = CalculateSlippage(openTrade.Direction == "Long" ? -1 : 1, request.SpreadPips);

                    var finalPnL = realizedPnL - commission - slippage;
                    balance += finalPnL;

                    var completedTrade = openTrade with
                    {
                        ExitTime = currentCandle.Time,
                        ExitPrice = exitResult.ExitPrice,
                        PnL = finalPnL,
                        PnLPercent = finalPnL / (openTrade.Units * openTrade.EntryPrice),
                        Commission = commission,
                        Slippage = slippage,
                        ExitReason = exitResult.Reason
                    };

                    trades.Add(completedTrade);
                    openTrade = null;

                    logger.LogInformation("📈 Trade #{TradeNum} closed: {Direction} {PnL:+0.00;-0.00;0} ({Reason})",
                        completedTrade.TradeNumber, completedTrade.Direction, completedTrade.PnL,
                        completedTrade.ExitReason);
                }
            }

            // Look for new entry signals
            if (openTrade != null) continue; // Already open trade

            var hasPosition = false;
            var isLong = false;
            var decision = request.Strategy.Evaluate(
                currentCandles.Select(MapToCandle).ToList().AsReadOnly(),
                currentCandle.Time, hasPosition, isLong);

            if(decision.Action != TradeAction.Buy && decision.Action != TradeAction.Sell) continue; // No decision

            // Calculate position size (robust against zero stop distance)
            var price = currentCandle.Close;
            var isBuy = decision.Action == TradeAction.Buy;

            // Determine pip size (simple rule: JPY pairs use 0.01, others 0.0001)
            decimal pipSize = GetPipSize(request.Instrument);
            // Minimum stop distance: e.g., 3 pips + 0.5 spread (in price units)
            var minStopPips = 3m;
            var minStopDistance = minStopPips * pipSize + (request.SpreadPips * 0.5m * pipSize);

            // Resolve a valid stop loss
            decimal? sl = decision.StopLoss;
            if (!sl.HasValue)
            {
                // Fallback: 1% move or at least the minimum stop distance
                var fallbackSl = isBuy
                    ? price - Math.Max(price * 0.01m, minStopDistance)
                    : price + Math.Max(price * 0.01m, minStopDistance);
                sl = fallbackSl;
            }

            var rawStopDistance = Math.Abs(price - sl.Value);
            var stopDistance = rawStopDistance < minStopDistance ? minStopDistance : rawStopDistance;

            // Risk-based sizing
            var riskAmount = request.InitialBalance > 0 ? (balance * request.RiskPerTrade) : 0m;
            if (riskAmount <= 0m) continue;

            // Units = risk in price units divided by stop distance
            var units = Math.Floor(stopDistance <= 0m ? 0m : (riskAmount / stopDistance));
            if (units <= 0) continue; // No position size available

            tradeNumber++;
            openTrade = new BacktestTrade
            {
                Id = Guid.NewGuid(),
                TradeNumber = tradeNumber,
                Instrument = request.Instrument,
                Direction = isBuy ? "Long" : "Short",
                EntryTime = currentCandle.Time,
                EntryPrice = price,
                Units = units,
                StopLoss = sl,
                TakeProfit = decision.TakeProfit
            };

            logger.LogInformation(
                "📊 Trade #{TradeNum} opened: {Direction} {Units} units @ {Price:F5} (SL {SL:F5}, StopDist {Dist:F5})",
                tradeNumber, openTrade.Direction, openTrade.Units, openTrade.EntryPrice, openTrade.StopLoss,
                stopDistance);
        }

        stopwatch.Stop();

        // Calculate final metrics
        var metrics = CalculateMetrics(trades, equityCurve, request.InitialBalance);

        var result = new BacktestResult
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            StrategyName = request.Strategy.Name,
            StrategyConfig = SerializeStrategy(request.Strategy),
            Instrument = request.Instrument,
            Timeframe = request.Timeframe,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            InitialBalance = request.InitialBalance,
            FinalBalance = balance,
            TotalReturn = (balance - request.InitialBalance) / request.InitialBalance,
            Trades = trades,
            EquityCurve = equityCurve,
            Metrics = metrics,
            Duration = stopwatch.Elapsed
        };

        logger.LogInformation(
            "✅ Backtest completed in {Duration}ms: {TotalReturn:P2} return, {Sharpe:F2} Sharpe, {Trades} trades",
            stopwatch.ElapsedMilliseconds, result.TotalReturn, result.Metrics.SharpeRatio, trades.Count);

        return result;
    }

    private static decimal GetPipSize(string instrument)
    {
        // Basic heuristic; adjust if you support exotic pairs or fractional pips
        return instrument.EndsWith("_JPY", StringComparison.OrdinalIgnoreCase) ? 0.01m : 0.0001m;
    }

    private BacktestMetrics CalculateMetrics(List<BacktestTrade> trades, List<EquityPoint> equityCurve,
        decimal initialBalance)
    {
        if (!trades.Any()) return new BacktestMetrics();

        var winners = trades.Where(t => t.PnL > 0).ToList();
        var losers = trades.Where(t => t.PnL <= 0).ToList();

        // Calculate returns for Sharpe ratio (use double pipeline for math functions)
        var dailyReturns = CalculateDailyReturns(equityCurve); // List<double>
        var avgDailyReturn = dailyReturns.Average(); // double
        var dailyVolatility = Math.Sqrt(dailyReturns.Sum(r => Math.Pow(r - avgDailyReturn, 2)) / dailyReturns.Count);
        var sharpeRatio = dailyVolatility == 0 ? 0 : (decimal)(avgDailyReturn / dailyVolatility * Math.Sqrt(252));

        // Sortino ratio (downside deviation only)
        var downsideReturns = dailyReturns.Where(r => r < 0).ToList();
        var downsideVolatility = downsideReturns.Any()
            ? Math.Sqrt(downsideReturns.Sum(r => Math.Pow(r, 2)) / downsideReturns.Count)
            : 0;
        var sortinoRatio = downsideVolatility == 0
            ? 0
            : (decimal)(avgDailyReturn / downsideVolatility * Math.Sqrt(252));

        // Maximum drawdown
        var maxDrawdown = equityCurve.Max(e => e.DrawdownPercent);

        // Calmar ratio
        var totalDays = (equityCurve.Last().Timestamp - equityCurve.First().Timestamp).TotalDays;
        var endEquity = equityCurve.Last().Equity;
        var annualizedReturn = totalDays <= 0
            ? 0d
            : Math.Pow((double)(endEquity / initialBalance), 365.0 / totalDays) - 1d;
        var calmarRatio = maxDrawdown == 0 ? 0 : (decimal)annualizedReturn / maxDrawdown;


        return new BacktestMetrics
        {
            TotalTrades = trades.Count,
            WinningTrades = winners.Count,
            LosingTrades = losers.Count,
            WinRate = (decimal)winners.Count / trades.Count,
            ProfitFactor = losers.Sum(t => Math.Abs(t.PnL)) == 0
                ? 0
                : winners.Sum(t => t.PnL) / losers.Sum(t => Math.Abs(t.PnL)),
            AverageWin = winners.Any() ? winners.Average(t => t.PnL) : 0,
            AverageLoss = losers.Any() ? losers.Average(t => t.PnL) : 0,
            LargestWin = winners.Any() ? winners.Max(t => t.PnL) : 0,
            LargestLoss = losers.Any() ? losers.Min(t => t.PnL) : 0,
            MaxDrawdown = maxDrawdown,
            SharpeRatio = sharpeRatio,
            SortinoRatio = sortinoRatio,
            CalmarRatio = calmarRatio,
            AverageTradeDuration =
                TimeSpan.FromMilliseconds(trades.Average(t => (t.ExitTime - t.EntryTime).TotalMilliseconds)),
            ProfitabilityIndex = trades.Count == 0
                ? 0
                : winners.Count * winners.Average(t => t.PnL) /
                  (trades.Count * Math.Abs(trades.Average(t => Math.Abs(t.PnL))))
        };
    }

    private (bool ShouldExit, decimal ExitPrice, string Reason) CheckExitConditions(BacktestTrade openTrade,
        BacktestCandle currentCandle)
    {
        // Check stop loss
        if (openTrade.StopLoss.HasValue)
        {
            if ((openTrade.Direction == "Long" && currentCandle.Low <= openTrade.StopLoss.Value) ||
                (openTrade.Direction == "Short" && currentCandle.High >= openTrade.StopLoss.Value))
            {
                return (true, openTrade.StopLoss.Value, "StopLoss");
            }
        }

        // Check take profit
        if (openTrade.TakeProfit.HasValue)
        {
            if ((openTrade.Direction == "Long" && currentCandle.High >= openTrade.TakeProfit.Value) ||
                (openTrade.Direction == "Short" && currentCandle.Low <= openTrade.TakeProfit.Value))
            {
                return (true, openTrade.TakeProfit.Value, "TakeProfit");
            }
        }

        // No exit condition met
        return (false, currentCandle.Close, "");
    }

    private decimal CalculateCommission(decimal units, decimal commissionRate)
    {
        // OANDA charges ~$5 per 100k lot
        var lots = Math.Abs(units) / 100000m;
        return lots * commissionRate;
    }

    private decimal CalculateSlippage(int direction, decimal spreadPips)
    {
        // Simple slippage model - half the spread
        return spreadPips * 0.5m * Math.Abs(direction);
    }

    private Candle MapToCandle(BacktestCandle backtestCandle)
    {
        return new Candle
        {
            Time = backtestCandle.Time,
            Open = backtestCandle.Open,
            High = backtestCandle.High,
            Low = backtestCandle.Low,
            Close = backtestCandle.Close,
            Volume = backtestCandle.Volume,
            Instrument = backtestCandle.Instrument,
            Granularity = backtestCandle.Timeframe
        };
    }

    private string SerializeStrategy(IStrategy strategy)
    {
        // Simple strategy serialization - you might want to make this more sophisticated
        return JsonSerializer.Serialize(new
        {
            strategy.Name,
            Type = strategy.GetType().Name,
            Timestamp = DateTime.UtcNow
        });
    }

    private List<double> CalculateDailyReturns(List<EquityPoint> equityCurve)
    {
        var dailyReturns = new List<double>();
        var dailyEquity = equityCurve
            .GroupBy(e => e.Timestamp.Date)
            .Select(g => new { Date = g.Key, g.OrderBy(e => e.Timestamp).Last().Equity })
            .OrderBy(d => d.Date)
            .ToList();

        for (int i = 1; i < dailyEquity.Count; i++)
        {
            // compute as double to keep math consistent with Math.Sqrt/Math.Pow pipeline
            var prev = (double)dailyEquity[i - 1].Equity;
            var curr = (double)dailyEquity[i].Equity;
            if (prev != 0)
            {
                var dailyReturn = (curr - prev) / prev;
                dailyReturns.Add(dailyReturn);
            }
        }

        return dailyReturns;
    }

    private double CalculateSortinoRatio(List<decimal> dailyReturns)
    {
        var avgReturn = (double)dailyReturns.Average();
        var downsideReturns = dailyReturns.Where(r => r < 0).ToList();

        if (!downsideReturns.Any()) return 0;

        var downsideDeviation = Math.Sqrt(downsideReturns.Sum(r => Math.Pow((double)r, 2)) / downsideReturns.Count);

        return downsideDeviation == 0 ? 0 : avgReturn / downsideDeviation * Math.Sqrt(252);
    }

    public async Task SaveBacktestResultAsync(BacktestResult result)
    {
        var dbRun = new BacktestRun
        {
            Id = result.Id,
            Name = result.Name,
            StrategyName = result.StrategyName,
            StrategyConfig = result.StrategyConfig,
            Instrument = result.Instrument,
            Timeframe = result.Timeframe,
            StartDate = result.StartDate,
            EndDate = result.EndDate,
            InitialBalance = result.InitialBalance,
            FinalBalance = result.FinalBalance,
            TotalReturn = result.TotalReturn,
            MaxDrawdown = result.Metrics.MaxDrawdown,
            SharpeRatio = result.Metrics.SharpeRatio,
            SortinoRatio = result.Metrics.SortinoRatio,
            CalmarRatio = result.Metrics.CalmarRatio,
            ProfitFactor = result.Metrics.ProfitFactor,
            WinRate = result.Metrics.WinRate,
            TotalTrades = result.Metrics.TotalTrades,
            WinningTrades = result.Metrics.WinningTrades,
            LosingTrades = result.Metrics.LosingTrades,
            AverageWin = result.Metrics.AverageWin,
            AverageLoss = result.Metrics.AverageLoss,
            LargestWin = result.Metrics.LargestWin,
            LargestLoss = result.Metrics.LargestLoss
        };

        await dbContext.BacktestRuns.AddAsync(dbRun);

        // Save trades (continued)
        var dbTrades = result.Trades.Select(t => new BacktestTradeEntity
        {
            BacktestRunId = result.Id,
            TradeNumber = t.TradeNumber,
            Instrument = t.Instrument,
            Direction = t.Direction,
            EntryTime = t.EntryTime,
            EntryPrice = t.EntryPrice,
            ExitTime = t.ExitTime,
            ExitPrice = t.ExitPrice,
            Units = t.Units,
            StopLoss = t.StopLoss,
            TakeProfit = t.TakeProfit,
            PnL = t.PnL,
            PnLPercent = t.PnLPercent,
            Commission = t.Commission,
            Slippage = t.Slippage,
            ExitReason = t.ExitReason,
            MAE = t.MAE,
            MFE = t.MFE
        });

        await dbContext.BacktestTrades.AddRangeAsync(dbTrades);

        // Save equity curve (sample every hour to reduce data size)
        var sampledEquityCurve = result.EquityCurve
            .Where((point, index) =>
                index % 12 == 0 || index == result.EquityCurve.Count - 1) // Every 12th point (1 hour for M5 data)
            .Select(e => new BacktestEquityPoint
            {
                BacktestRunId = result.Id,
                Timestamp = e.Timestamp,
                Balance = e.Balance,
                Equity = e.Equity,
                DrawdownPercent = e.DrawdownPercent
            });

        await dbContext.BacktestEquityCurve.AddRangeAsync(sampledEquityCurve);

        await dbContext.SaveChangesAsync();

        logger.LogInformation("💾 Saved backtest result: {Name} with {TradeCount} trades", result.Name,
            result.Trades.Count);
    }

    public async Task<BacktestResult> GetBacktestResultAsync(Guid backtestId)
    {
        var dbRun = await dbContext.BacktestRuns
            .Include(r => r.Trades.OrderBy(t => t.TradeNumber))
            .Include(r => r.EquityCurve.OrderBy(e => e.Timestamp))
            .FirstOrDefaultAsync(r => r.Id == backtestId);

        if (dbRun == null)
            throw new ArgumentException($"Backtest with ID {backtestId} not found");

        var trades = dbRun.Trades.Select(t => new BacktestTrade
        {
            Id = t.Id,
            BacktestRunId = t.BacktestRunId,
            TradeNumber = t.TradeNumber,
            Instrument = t.Instrument,
            Direction = t.Direction,
            EntryTime = t.EntryTime,
            EntryPrice = t.EntryPrice,
            ExitTime = t.ExitTime,
            ExitPrice = t.ExitPrice,
            Units = t.Units,
            StopLoss = t.StopLoss,
            TakeProfit = t.TakeProfit,
            PnL = t.PnL,
            PnLPercent = t.PnLPercent,
            Commission = t.Commission,
            Slippage = t.Slippage,
            ExitReason = t.ExitReason,
            MAE = t.MAE,
            MFE = t.MFE
        }).ToList();

        var equityCurve = dbRun.EquityCurve.Select(e => new EquityPoint(
            e.Timestamp, e.Balance, e.Equity, e.DrawdownPercent
        )).ToList();

        var metrics = new BacktestMetrics
        {
            TotalTrades = dbRun.TotalTrades,
            WinningTrades = dbRun.WinningTrades,
            LosingTrades = dbRun.LosingTrades,
            WinRate = dbRun.WinRate ?? 0,
            ProfitFactor = dbRun.ProfitFactor ?? 0,
            AverageWin = dbRun.AverageWin ?? 0,
            AverageLoss = dbRun.AverageLoss ?? 0,
            LargestWin = dbRun.LargestWin ?? 0,
            LargestLoss = dbRun.LargestLoss ?? 0,
            MaxDrawdown = dbRun.MaxDrawdown,
            SharpeRatio = dbRun.SharpeRatio ?? 0,
            SortinoRatio = dbRun.SortinoRatio ?? 0,
            CalmarRatio = dbRun.CalmarRatio ?? 0
        };

        return new BacktestResult
        {
            Id = dbRun.Id,
            Name = dbRun.Name,
            StrategyName = dbRun.StrategyName,
            StrategyConfig = dbRun.StrategyConfig,
            Instrument = dbRun.Instrument,
            Timeframe = dbRun.Timeframe,
            StartDate = dbRun.StartDate,
            EndDate = dbRun.EndDate,
            InitialBalance = dbRun.InitialBalance,
            FinalBalance = dbRun.FinalBalance,
            TotalReturn = dbRun.TotalReturn,
            Trades = trades,
            EquityCurve = equityCurve,
            Metrics = metrics,
            CreatedAt = dbRun.CreatedAt
        };
    }

    // Task<BacktestResult> IBacktestEngine.RunBacktestAsync(BacktestRequest request, CancellationToken cancellationToken)
    // {
    //     return RunBacktestAsync(request, cancellationToken);
    // }

    public async Task<List<BacktestResult>> CompareStrategiesAsync(CompareStrategiesRequest request,
        CancellationToken cancellationToken = default)
    {
        var results = new List<BacktestResult>();

        foreach (var strategy in request.Strategies)
        {
            var backtestRequest = new BacktestRequest(
                Name: $"Compare-{strategy.Name}-{request.Instrument}",
                Strategy: strategy,
                Instrument: request.Instrument,
                Timeframe: request.Timeframe,
                StartDate: request.StartDate,
                EndDate: request.EndDate,
                InitialBalance: request.InitialBalance
            );

            try
            {
                var result = await RunBacktestAsync(backtestRequest, cancellationToken);
                results.Add(result);

                // Save each comparison result
                await SaveBacktestResultAsync(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Strategy comparison failed for {Strategy}", strategy.Name);
            }
        }

        return results;
    }
}