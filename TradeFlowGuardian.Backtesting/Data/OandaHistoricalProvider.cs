using TradeFlowGuardian.Backtesting.Data.Entities;
using TradeFlowGuardian.Backtesting.Models;
using TradeFlowGuardian.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TradeFlowGuardian.Backtesting.Data;

public class OandaHistoricalProvider(
    IOandaApiService oandaService,
    BacktestDataContext dbContext,
    ILogger<OandaHistoricalProvider> logger)
    : IHistoricalDataProvider
{
    public async Task<List<BacktestCandle>> GetHistoricalDataAsync(string instrument, string timeframe,
        DateTime startDate, DateTime endDate)
    {
        logger.LogInformation("🔍 Loading historical data for {Instrument} from {Start} to {End}",
            instrument, startDate, endDate);

        // First, try to get data from database
        var dbCandles = await dbContext.HistoricalCandles
            .Where(c => c.Instrument == instrument &&
                        c.Timeframe == timeframe &&
                        c.Timestamp >= startDate &&
                        c.Timestamp <= endDate)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        var result = dbCandles.Select(c => new BacktestCandle(
            c.Timestamp, c.Open, c.High, c.Low, c.Close, c.Volume, c.Instrument, c.Timeframe
        )).ToList();

        logger.LogInformation("📊 Loaded {Count} candles from database", result.Count);

        // Check if we have gaps and need to fetch from OANDA
        if (result.Count == 0 || HasDataGaps(result, startDate, endDate, timeframe))
        {
            logger.LogWarning("⚠️ Data gaps detected, fetching from OANDA API...");
            await FillDataGapsAsync(instrument, timeframe, startDate, endDate, result);
        }

        return result.OrderBy(c => c.Time).ToList();
    }

    private bool HasDataGaps(List<BacktestCandle> candles, DateTime startDate, DateTime endDate, string timeframe)
    {
        if (!candles.Any()) return true;

        var expectedMinutes = timeframe switch
        {
            "M1" => 1,
            "M5" => 5,
            "M15" => 15,
            "M30" => 30,
            "H1" => 60,
            "H4" => 240,
            "D" => 1440,
            _ => 5
        };

        var current = startDate;
        var candleIndex = 0;

        while (current <= endDate && candleIndex < candles.Count)
        {
            // Skip weekends for forex data
            if (current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday)
            {
                current = current.AddDays(1);
                continue;
            }

            var expectedCandle =
                candles.FirstOrDefault(c => Math.Abs((c.Time - current).TotalMinutes) < expectedMinutes);
            if (expectedCandle == null)
            {
                logger.LogDebug("Gap found at {Time}", current);
                return true;
            }

            current = current.AddMinutes(expectedMinutes);
            candleIndex++;
        }

        return false;
    }

    private async Task FillDataGapsAsync(string instrument, string timeframe, DateTime startDate, DateTime endDate,
        List<BacktestCandle> existingCandles)
    {
        try
        {
            // Choose chunk size so that each request returns <= 5000 candles
            var minutesPerCandle = timeframe switch
            {
                "M1" => 1,
                "M5" => 5,
                "M15" => 15,
                "M30" => 30,
                "H1" => 60,
                "H4" => 240,
                "D" => 1440,
                _ => 5 // default to M5 granularity if unknown
            };

            // Max minutes per request to stay within 5000-candle limit
            var maxMinutes = 5000 * minutesPerCandle;
            // Use a slight margin to avoid off-by-one at boundaries
            var chunkSpan = TimeSpan.FromMinutes(Math.Max(1, maxMinutes - minutesPerCandle));

            var currentStart = startDate.ToUniversalTime();

            while (currentStart < endDate.ToUniversalTime())
            {
                var candidateEnd = currentStart.Add(chunkSpan);
                var chunkEnd = candidateEnd > endDate ? endDate.ToUniversalTime() : candidateEnd;

                // Optional: small 1-candle overlap to avoid gaps at boundaries
                var overlap = TimeSpan.FromMinutes(minutesPerCandle);
                var requestStart = currentStart;
                var requestEnd = chunkEnd.Add(overlap);

                logger.LogInformation("📥 Fetching chunk from OANDA: {Start} to {End}",
                    requestStart, requestEnd);

                // IMPORTANT: request by time window (no count)
                var oandaCandles = await oandaService.GetCandlesAsync(
                    instrument, timeframe, requestStart, requestEnd, includeIncomplete: false);

                // Convert to BacktestCandle, filter strictly to [currentStart, chunkEnd]
                var chunkCandles = oandaCandles
                    .Where(c => c.Time >= currentStart && c.Time <= chunkEnd)
                    .Select(c =>
                        new BacktestCandle(c.Time, c.Open, c.High, c.Low, c.Close, c.Volume, instrument, timeframe))
                    .OrderBy(c => c.Time)
                    .ToList();

                if (chunkCandles.Count == 0)
                {
                    logger.LogInformation("⏭️ No candles returned for {Start}..{End}", currentStart, chunkEnd);
                }
                else
                {
                    // De-duplicate against existingCandles in-memory to avoid double inserts with overlap
                    var existingSet = existingCandles
                        .Where(c => c.Timeframe == timeframe && c.Instrument == instrument)
                        .Select(c => c.Time)
                        .ToHashSet();

                    var newOnes = chunkCandles
                        .Where(c => !existingSet.Contains(c.Time))
                        .ToList();

                    existingCandles.AddRange(newOnes);

                    if (newOnes.Any())
                    {
                        var dbCandles = newOnes.Select(c => new HistoricalCandle
                        {
                            Instrument = instrument,
                            Timeframe = timeframe,
                            Timestamp = c.Time,
                            Open = c.Open,
                            High = c.High,
                            Low = c.Low,
                            Close = c.Close,
                            Volume = c.Volume
                        });

                        await dbContext.HistoricalCandles.AddRangeAsync(dbCandles);
                        await dbContext.SaveChangesAsync();

                        logger.LogInformation("💾 Saved {Count} candles to database", newOnes.Count);
                    }
                    else
                    {
                        logger.LogInformation("⏭️ All candles for chunk already present (after de-dup)");
                    }
                }

                currentStart = chunkEnd; // advance to end of chunk
                await Task.Delay(500); // polite pacing
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch historical data from OANDA");
            throw;
        }
    }

    public async Task<bool> IsDataAvailableAsync(string instrument, string timeframe, DateTime startDate,
        DateTime endDate)
    {
        var count = await dbContext.HistoricalCandles
            .Where(c => c.Instrument == instrument &&
                        c.Timeframe == timeframe &&
                        c.Timestamp >= startDate &&
                        c.Timestamp <= endDate)
            .CountAsync();

        var expectedCandles = CalculateExpectedCandleCount(startDate, endDate, timeframe);
        var availabilityPercent = (decimal)count / expectedCandles;

        return availabilityPercent >= 0.8m; // Consider 80%+ availability as "available"
    }

    public async Task<DateTime?> GetEarliestDataDateAsync(string instrument, string timeframe)
    {
        return await dbContext.HistoricalCandles
            .Where(c => c.Instrument == instrument && c.Timeframe == timeframe)
            .MinAsync(c => (DateTime?)c.Timestamp);
    }

    public async Task<DateTime?> GetLatestDataDateAsync(string instrument, string timeframe)
    {
        return await dbContext.HistoricalCandles
            .Where(c => c.Instrument == instrument && c.Timeframe == timeframe)
            .MaxAsync(c => (DateTime?)c.Timestamp);
    }

    private int CalculateExpectedCandleCount(DateTime startDate, DateTime endDate, string timeframe)
    {
        var totalMinutes = (endDate - startDate).TotalMinutes;
        var candleMinutes = timeframe switch
        {
            "M1" => 1,
            "M5" => 5,
            "M15" => 15,
            "M30" => 30,
            "H1" => 60,
            "H4" => 240,
            "D" => 1440,
            _ => 5
        };

        // Rough estimate excluding weekends (forex doesn't trade weekends)
        var totalCandles = (int)(totalMinutes / candleMinutes);
        var weekendAdjustment = 0.7m; // Roughly 70% of time is trading time

        return (int)(totalCandles * weekendAdjustment);
    }
}