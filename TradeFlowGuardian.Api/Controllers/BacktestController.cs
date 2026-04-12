using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeFlowGuardian.Backtesting.Data;
using TradeFlowGuardian.Backtesting.Engine;
using TradeFlowGuardian.Backtesting.Models;
using TradeFlowGuardian.Backtesting.Strategies;

namespace TradeFlowGuardian.Api.Controllers;

[ApiController]
[Route("api/backtest")]
public class BacktestController(
    IBacktestEngine engine,
    IHistoricalDataProvider dataProvider,
    BacktestDataContext db,
    ILogger<BacktestController> logger) : ControllerBase
{
    /// <summary>
    /// Runs a backtest and persists the result.
    /// Fetches historical candles from OANDA (cached in PostgreSQL after first run).
    /// Note: large date ranges over short timeframes may take 30–120 s.
    /// </summary>
    [HttpPost("run")]
    [ProducesResponseType(typeof(BacktestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RunBacktest(
        [FromBody] BacktestApiRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var strategy = StrategyFactory.Create(
                request.StrategyPreset,
                request.FastPeriods,
                request.SlowPeriods);

            var backtestRequest = new BacktestRequest(
                Name:           request.Name,
                Strategy:       strategy,
                Instrument:     request.Instrument,
                Timeframe:      request.Timeframe,
                StartDate:      request.StartDate,
                EndDate:        request.EndDate,
                InitialBalance: request.InitialBalance,
                RiskPerTrade:   request.RiskPerTrade,
                Commission:     request.Commission,
                SpreadPips:     request.SpreadPips);

            logger.LogInformation(
                "Backtest starting: {Name} | {Preset} | {Instrument} {Timeframe} {Start:d}–{End:d}",
                request.Name, request.StrategyPreset, request.Instrument,
                request.Timeframe, request.StartDate, request.EndDate);

            var result = await engine.RunBacktestAsync(backtestRequest, cancellationToken);
            await engine.SaveBacktestResultAsync(result);

            logger.LogInformation(
                "Backtest complete: {Name} | Trades={Trades} WinRate={WinRate:P1} Sharpe={Sharpe:F2} MaxDD={MaxDD:P1}",
                result.Name, result.Metrics.TotalTrades, result.Metrics.WinRate,
                result.Metrics.SharpeRatio, result.Metrics.MaxDrawdown);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lists all saved backtest runs, newest first.
    /// Returns summary rows only — no trades or equity curve.
    /// </summary>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(List<BacktestRunSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRuns([FromQuery] int limit = 50)
    {
        var runs = await db.BacktestRuns
            .OrderByDescending(r => r.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(r => new BacktestRunSummary(
                r.Id,
                r.Name,
                r.StrategyName,
                r.Instrument,
                r.Timeframe,
                r.StartDate,
                r.EndDate,
                r.InitialBalance,
                r.FinalBalance,
                r.TotalReturn,
                r.MaxDrawdown,
                r.SharpeRatio,
                r.WinRate,
                r.TotalTrades,
                r.CreatedAt))
            .ToListAsync();

        return Ok(runs);
    }

    /// <summary>
    /// Loads a previously saved backtest result by ID (includes trades and equity curve).
    /// </summary>
    [HttpGet("runs/{id:guid}")]
    [ProducesResponseType(typeof(BacktestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRun(Guid id)
    {
        try
        {
            var result = await engine.GetBacktestResultAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { error = $"Backtest run {id} not found." });
        }
    }

    /// <summary>
    /// Returns the list of supported strategy preset names.
    /// </summary>
    [HttpGet("strategies")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public IActionResult GetStrategies() => Ok(StrategyFactory.SupportedPresets);

    /// <summary>
    /// Reports how much historical data is cached for a given instrument/timeframe/period.
    /// Run this before a backtest to confirm data quality.
    /// Returns coverage %, candle counts, and the earliest/latest dates available in the DB.
    /// A coverage below 80% means the backtest engine will trigger a live OANDA fetch.
    /// </summary>
    [HttpGet("data/coverage")]
    [ProducesResponseType(typeof(DataCoverageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDataCoverage(
        [FromQuery] string instrument,
        [FromQuery] string timeframe,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (string.IsNullOrWhiteSpace(instrument) || string.IsNullOrWhiteSpace(timeframe))
            return BadRequest(new { error = "instrument and timeframe are required." });

        if (endDate <= startDate)
            return BadRequest(new { error = "endDate must be after startDate." });

        var candlesFound = await db.HistoricalCandles
            .Where(c => c.Instrument == instrument
                     && c.Timeframe == timeframe
                     && c.Timestamp >= startDate
                     && c.Timestamp <= endDate)
            .CountAsync();

        var candlesExpected = CalculateExpectedCandleCount(startDate, endDate, timeframe);
        var coveragePct = candlesExpected == 0
            ? 0m
            : Math.Round((decimal)candlesFound / candlesExpected * 100, 1);

        var earliest = await dataProvider.GetEarliestDataDateAsync(instrument, timeframe);
        var latest   = await dataProvider.GetLatestDataDateAsync(instrument, timeframe);

        return Ok(new DataCoverageResponse(
            Instrument:       instrument,
            Timeframe:        timeframe,
            StartDate:        startDate,
            EndDate:          endDate,
            CandlesFound:     candlesFound,
            CandlesExpected:  candlesExpected,
            CoveragePercent:  coveragePct,
            IsAvailable:      coveragePct >= 80m,
            EarliestCached:   earliest,
            LatestCached:     latest));
    }

    /// <summary>Candle count estimate: total minutes / candle-minutes × 0.71 (weekend exclusion).</summary>
    private static int CalculateExpectedCandleCount(DateTime start, DateTime end, string timeframe)
    {
        var candleMinutes = timeframe switch
        {
            "M1"  => 1,
            "M5"  => 5,
            "M15" => 15,
            "M30" => 30,
            "H1"  => 60,
            "H4"  => 240,
            "D"   => 1440,
            _     => 5
        };
        var totalMinutes = (end - start).TotalMinutes;
        // ~71% of calendar time is forex trading time (Mon 17:00 – Fri 17:00 EST)
        return (int)(totalMinutes / candleMinutes * 0.71);
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <summary>Inbound request for POST /api/backtest/run.</summary>
public record BacktestApiRequest
{
    /// <summary>Human-readable label for this run (e.g., "EUR/USD H1 2024").</summary>
    public required string Name { get; init; }

    /// <summary>OANDA instrument code (e.g., EUR_USD, USD_JPY, GBP_USD).</summary>
    public string Instrument { get; init; } = "EUR_USD";

    /// <summary>OANDA granularity (M1, M5, M15, M30, H1, H4, D).</summary>
    public string Timeframe { get; init; } = "H1";

    public DateTime StartDate { get; init; }
    public DateTime EndDate   { get; init; }

    /// <summary>
    /// Strategy preset name. One of: emac_10_30, emac_9_21, emac_12_26, emac_custom.
    /// Use emac_custom together with FastPeriods / SlowPeriods for ad-hoc EMA periods.
    /// </summary>
    public string StrategyPreset { get; init; } = "emac_10_30";

    /// <summary>Fast EMA period — only used when StrategyPreset is "emac_custom".</summary>
    public int? FastPeriods { get; init; }

    /// <summary>Slow EMA period — only used when StrategyPreset is "emac_custom".</summary>
    public int? SlowPeriods { get; init; }

    /// <summary>Starting account balance in USD.</summary>
    public decimal InitialBalance { get; init; } = 10_000m;

    /// <summary>Fraction of balance risked per trade (e.g., 0.01 = 1%).</summary>
    public decimal RiskPerTrade { get; init; } = 0.01m;

    /// <summary>Round-trip commission in USD per 100k lot.</summary>
    public decimal Commission { get; init; } = 7m;

    /// <summary>Assumed spread in pips added to entry cost.</summary>
    public decimal SpreadPips { get; init; } = 0.5m;
}

/// <summary>Lightweight summary row returned by GET /api/backtest/runs.</summary>
public record BacktestRunSummary(
    Guid     Id,
    string   Name,
    string   StrategyName,
    string   Instrument,
    string   Timeframe,
    DateTime StartDate,
    DateTime EndDate,
    decimal  InitialBalance,
    decimal  FinalBalance,
    decimal  TotalReturn,
    decimal  MaxDrawdown,
    decimal? SharpeRatio,
    decimal? WinRate,
    int      TotalTrades,
    DateTime CreatedAt);

/// <summary>Response for GET /api/backtest/data/coverage.</summary>
public record DataCoverageResponse(
    string    Instrument,
    string    Timeframe,
    DateTime  StartDate,
    DateTime  EndDate,
    int       CandlesFound,
    int       CandlesExpected,
    decimal   CoveragePercent,
    bool      IsAvailable,
    DateTime? EarliestCached,
    DateTime? LatestCached);
