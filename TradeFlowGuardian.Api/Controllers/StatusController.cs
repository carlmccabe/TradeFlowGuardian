using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TradeFlowGuardian.Api.Hubs;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Brokers;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using TradeFlowGuardian.Infrastructure.Data;
using StackExchange.Redis;
using System.Reflection;

namespace TradeFlowGuardian.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class StatusController(
    IBrokerClient broker,
    IPauseState pauseState,
    IDailyDrawdownGuard drawdownGuard,
    ITradeHistoryRepository tradeHistory,
    IRiskSettingsRepository riskSettingsRepo,
    IActiveAccountProvider activeAccount,
    IConnectionMultiplexer redis,
    IOptions<RiskConfig> risk,
    IHubContext<TradingHub> hub,
    ILogger<StatusController> logger) : ControllerBase
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    private static string GitSha =>
        Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA")
        ?? Environment.GetEnvironmentVariable("GIT_SHA")
        ?? "unknown";

    /// <summary>
    /// Identifies the running Api build: git SHA, start time, and the active broker
    /// account's environment. First stop after a deploy — does the SHA match what you merged?
    /// </summary>
    [HttpGet("version")]
    public async Task<IActionResult> GetVersion(CancellationToken ct)
    {
        string? accountEnv = null, accountLabel = null;
        try
        {
            var acct = await activeAccount.GetActiveAsync(ct);
            accountEnv   = acct.Environment;
            accountLabel = acct.Label;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Version endpoint could not resolve active account");
        }

        return Ok(new
        {
            service      = "api",
            sha          = GitSha,
            startedAt    = StartedAt,
            uptimeSeconds = (long)(DateTimeOffset.UtcNow - StartedAt).TotalSeconds,
            accountEnvironment = accountEnv,
            accountLabel,
            isLive       = accountEnv == "fxtrade",
            fetchedAt    = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Deploy readiness: exercises every dependency the trade pipeline needs and reports
    /// each check individually — Postgres + schema version vs the migrations shipped in
    /// this build, Redis, broker balance, per-instrument risk settings (the rows the sizer
    /// reads; a missing row silently falls back to the config default), and the Worker
    /// heartbeat with the SHA it is running. ok=true only when everything passes.
    /// </summary>
    [HttpGet("readiness")]
    public async Task<IActionResult> GetReadiness(CancellationToken ct)
    {
        // Postgres + schema level
        var (dbReachable, _, dbError) = await tradeHistory.GetStatusAsync(ct);
        var appliedVersion  = await tradeHistory.GetSchemaVersionAsync(ct);
        int? expectedVersion = null;
        try
        {
            expectedVersion = SqlMigrationRunner.LoadEmbedded(Assembly.GetExecutingAssembly())
                .Select(m => m.Version).DefaultIfEmpty(0).Max();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not enumerate embedded migrations");
        }
        var schemaCurrent = appliedVersion is not null && appliedVersion == expectedVersion;

        // Redis
        bool redisOk = false; string? redisError = null;
        try { await redis.GetDatabase().PingAsync(); redisOk = true; }
        catch (Exception ex) { redisError = ex.Message; }

        // Broker + account
        string? accountEnv = null, accountLabel = null; decimal balance = 0;
        string? brokerError = null;
        try
        {
            var acct = await activeAccount.GetActiveAsync(ct);
            accountEnv   = acct.Environment;
            accountLabel = acct.Label;
            balance      = await broker.GetAccountBalanceAsync(ct);
        }
        catch (Exception ex) { brokerError = ex.Message; }
        var brokerOk = brokerError is null && balance > 0;

        // Risk settings — the exact rows PositionSizer reads. No row for an instrument
        // means the sizer silently falls back to the config default risk %.
        var riskRows = await riskSettingsRepo.GetAllAsync(ct);
        var riskOk = riskRows.Count > 0;

        // Worker heartbeat
        object? worker = null; var workerOk = false;
        try
        {
            var raw = await redis.GetDatabase().StringGetAsync("tradeflow:worker:heartbeat");
            if (raw.HasValue)
            {
                using var doc = System.Text.Json.JsonDocument.Parse((string)raw!);
                var beatAt = doc.RootElement.GetProperty("beatAt").GetDateTimeOffset();
                var age    = (DateTimeOffset.UtcNow - beatAt).TotalSeconds;
                workerOk   = age < 60;
                worker = new
                {
                    sha = doc.RootElement.GetProperty("sha").GetString(),
                    startedAt = doc.RootElement.GetProperty("startedAt").GetDateTimeOffset(),
                    beatAgeSeconds = Math.Round(age, 1),
                    healthy = workerOk
                };
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "Could not read worker heartbeat"); }

        var ok = dbReachable && schemaCurrent && redisOk && brokerOk && riskOk && workerOk;

        return Ok(new
        {
            ok,
            api = new { sha = GitSha, startedAt = StartedAt },
            worker = worker ?? (object)new { healthy = false, error = "no heartbeat in Redis (worker down or old build without heartbeat)" },
            postgres = new { reachable = dbReachable, error = dbError, appliedMigration = appliedVersion, expectedMigration = expectedVersion, schemaCurrent },
            redis = new { reachable = redisOk, error = redisError },
            broker = new { reachable = brokerOk, error = brokerError, balanceAud = balance, accountEnvironment = accountEnv, accountLabel, isLive = accountEnv == "fxtrade" },
            riskSettings = new
            {
                ok = riskOk,
                note = riskOk ? null : "no rows — PositionSizer will fall back to config default risk %",
                instruments = riskRows.Select(r => new { r.Instrument, r.RiskPercent, r.IsActive, source = "db" })
            },
            fetchedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Fetches a dry-run signal's outcome by its idempotency key. The Worker stores results
    /// for 15 minutes. 404 until the Worker has processed the signal — poll after POSTing
    /// a signal with "dryRun": true.
    /// </summary>
    [HttpGet("dryrun/{key}")]
    public async Task<IActionResult> GetDryRunResult(string key, CancellationToken ct)
    {
        var raw = await redis.GetDatabase().StringGetAsync($"tradeflow:dryrun:{key}");
        if (!raw.HasValue)
            return NotFound(new { error = "no result yet — worker still processing, or key unknown/expired (15 min TTL)", key });
        return Content((string)raw!, "application/json");
    }

    /// <summary>
    /// Combined status: live balance + all open positions with unrealised P&amp;L.
    /// Each position is enriched with the SL/TP and sizing breakdown recorded when
    /// its entry order was placed (null when no matching local history row exists),
    /// plus projected AUD outcomes at the stop and target.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var balance   = await broker.GetAccountBalanceAsync(ct);
        var positions = await broker.GetAllOpenPositionsAsync(ct);

        // Latest still-open entry per instrument from local history (open = no Close fill yet).
        var recentTrades = await tradeHistory.GetPairedTradesAsync(
            DateTimeOffset.UtcNow.AddDays(-90), null, ct);
        var openEntries = recentTrades
            .Where(t => t.ClosedAt is null)
            .GroupBy(t => t.Instrument)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.OpenedAt).First());

        return Ok(new
        {
            balanceAud = balance,
            positions  = positions.Select(p =>
            {
                openEntries.TryGetValue(p.Instrument, out var entry);
                return new
                {
                    instrument   = p.Instrument,
                    units        = p.Units,
                    unrealizedPL = p.UnrealizedPL,
                    averagePrice = p.AveragePrice,
                    side         = p.Units switch { > 0 => "LONG", < 0 => "SHORT", _ => "FLAT" },
                    stopLoss     = entry?.StopLoss,
                    takeProfit   = entry?.TakeProfit,
                    projectedLossAud   = ProjectedAud(entry, entry?.StopLoss),
                    projectedProfitAud = ProjectedAud(entry, entry?.TakeProfit),
                    riskPercent  = entry?.RiskPercent,
                    riskAmount   = entry?.RiskAmount
                };
            }),
            fetchedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// AUD magnitude of the move from entry to <paramref name="level"/> for the trade's units.
    /// Null when the trade predates sizing transparency (no stored quote→AUD rate).
    /// </summary>
    private static decimal? ProjectedAud(PairedTradeRecord? trade, decimal? level)
    {
        if (trade is null || level is null || trade.QuoteToAud is null) return null;
        var entry = trade.EntryPrice > 0 ? trade.EntryPrice : 0m;
        if (entry <= 0) return null;
        return Math.Round(Math.Abs(level.Value - entry) * Math.Abs(trade.Units) * trade.QuoteToAud.Value, 2);
    }

    /// <summary>
    /// Entry+close trade pairs from PostgreSQL, with the sizing breakdown recorded at
    /// order time and projected AUD outcomes at the stop and target.
    /// Window selection: ?range=week|month|quarter|all (rolling 7/30/90 days or everything),
    /// or explicit ?from=YYYY-MM-DD&amp;to=YYYY-MM-DD (UTC; to is exclusive of that day's end).
    /// Explicit dates win over range. Default: last 90 days.
    /// </summary>
    [HttpGet("trades")]
    public async Task<IActionResult> GetTrades(
        [FromQuery] string? range = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        DateTimeOffset? fromUtc;
        DateTimeOffset? toUtc = null;

        if (from.HasValue || to.HasValue)
        {
            fromUtc = from.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(from.Value, DateTimeKind.Utc)) : null;
            // 'to' is a date the user wants included — advance to the next midnight (exclusive upper bound).
            toUtc = to.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc)) : null;
        }
        else
        {
            fromUtc = (range?.ToLowerInvariant()) switch
            {
                "week"    => DateTimeOffset.UtcNow.AddDays(-7),
                "month"   => DateTimeOffset.UtcNow.AddDays(-30),
                "quarter" => DateTimeOffset.UtcNow.AddDays(-90),
                "all"     => null,
                _         => DateTimeOffset.UtcNow.AddDays(-90)
            };
        }

        var trades = await tradeHistory.GetPairedTradesAsync(fromUtc, toUtc, ct);
        return Ok(trades.Select(t => new
        {
            instrument      = t.Instrument,
            direction       = t.Direction,
            entryPrice      = t.EntryPrice,
            exitPrice       = t.ExitPrice,
            units           = t.Units,
            openedAt        = t.OpenedAt,
            closedAt        = t.ClosedAt,
            durationSeconds = t.DurationSeconds,
            stopLoss        = t.StopLoss,
            takeProfit      = t.TakeProfit,
            projectedLossAud   = ProjectedAud(t, t.StopLoss),
            projectedProfitAud = ProjectedAud(t, t.TakeProfit),
            sizing = t.RiskPercent is null ? null : new
            {
                riskPercent    = t.RiskPercent,
                riskSource     = t.RiskSource,
                accountBalance = t.AccountBalance,
                riskAmount     = t.RiskAmount,
                atr            = t.Atr,
                stopDistance   = t.StopDistance,
                stopSource     = t.StopSource,
                quoteToAud     = t.QuoteToAud,
                capReason      = t.CapReason
            }
        }));
    }

    /// <summary>Returns all instruments with an open position.</summary>
    [HttpGet("positions")]
    public async Task<IActionResult> GetPositions(CancellationToken ct)
    {
        var positions = await broker.GetAllOpenPositionsAsync(ct);
        return Ok(positions.Select(p => new
        {
            instrument = p.Instrument,
            units = p.Units,
            unrealizedPL = p.UnrealizedPL,
            averagePrice = p.AveragePrice
        }));
    }

    /// <summary>
    /// Realized P&amp;L per UTC day for the current week (range=week, Monday→now) or the
    /// current month (range=month, 1st→now). Buckets by the trade's close date, so each
    /// bucket reflects P&amp;L actually realized that day. Only fully-closed trades count
    /// (entry fill paired with a Close fill). Returns an empty array when there is no
    /// matching trade history — never an error.
    /// </summary>
    [HttpGet("pnl")]
    public async Task<IActionResult> GetDailyPnl([FromQuery] string range = "week", CancellationToken ct = default)
    {
        var pnlRange = "month".Equals(range, StringComparison.OrdinalIgnoreCase)
            ? PnlRange.Month
            : PnlRange.Week;
        var data = await tradeHistory.GetDailyPnlAsync(pnlRange, ct);
        return Ok(data.Select(d => new
        {
            date       = d.Date,
            pnl        = d.Pnl,
            tradeCount = d.TradeCount,
        }));
    }

    /// <summary>
    /// Sets or clears the global pause flag. When paused, all new Long/Short entries
    /// are blocked in the Worker until explicitly resumed.
    /// </summary>
    [HttpPost("pause")]
    public async Task<IActionResult> SetPause([FromBody] SetPauseRequest body, CancellationToken ct)
    {
        await pauseState.SetPausedAsync(body.Paused, ct);
        logger.LogWarning("Global pause set to {Paused} via API", body.Paused);
        await hub.Clients.All.SendAsync("event", JsonSerializer.Serialize(new
        {
            type   = "pause_changed",
            paused = body.Paused
        }), ct);
        return Ok(new { paused = body.Paused, updatedAt = DateTimeOffset.UtcNow });
    }

    /// <summary>Returns live account balance from OANDA.</summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance(CancellationToken ct)
    {
        var balance = await broker.GetAccountBalanceAsync(ct);
        return Ok(new { balanceAud = balance, fetchedAt = DateTimeOffset.UtcNow });
    }

    /// <summary>Returns open position units for a given instrument.</summary>
    [HttpGet("position/{instrument}")]
    public async Task<IActionResult> GetPosition(string instrument, CancellationToken ct)
    {
        var units = await broker.GetOpenPositionUnitsAsync(instrument, ct);
        return Ok(new
        {
            instrument,
            units,
            side = units switch
            {
                > 0 => "LONG",
                < 0 => "SHORT",
                _ => "FLAT"
            },
            fetchedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Returns current filter status including the daily drawdown circuit breaker state.
    /// Used by the dashboard to show whether trading is paused.
    /// </summary>
    [HttpGet("filters")]
    public async Task<IActionResult> GetFilterStatus(CancellationToken ct)
    {
        var paused    = await pauseState.IsPausedAsync(ct);
        var isBreached = await drawdownGuard.IsBreachedAsync(ct);
        var dayOpenNav = await drawdownGuard.GetDayOpenNavAsync(ct);

        decimal? currentBalance = null;
        decimal? drawdownPercent = null;

        if (dayOpenNav.HasValue)
        {
            currentBalance = await broker.GetAccountBalanceAsync(ct);
            if (dayOpenNav.Value > 0)
                drawdownPercent = (dayOpenNav.Value - currentBalance.Value) / dayOpenNav.Value * 100m;
        }

        return Ok(new
        {
            paused,
            dailyDrawdown = new
            {
                isBreached,
                dayOpenNav,
                currentBalance,
                drawdownPercent,
                maxDrawdownPercent = risk.Value.MaxDailyDrawdownPercent,
                tradingDay = DateOnly.FromDateTime(DateTime.UtcNow)
            },
            fetchedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Returns closed trade history pulled directly from OANDA (not the local DB).
    /// RealizedPL is in account currency (AUD) as reported by OANDA.
    /// Returns an empty array when OANDA is unreachable or the account has no closed trades
    /// in the requested window — never throws.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetOandaHistory([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var from   = DateTimeOffset.UtcNow.AddDays(-days);
        var to     = DateTimeOffset.UtcNow;
        var trades = await broker.GetTransactionsAsync(from, to, ct);
        return Ok(trades.Select(t => new
        {
            id         = t.Id,
            instrument = t.Instrument,
            units      = t.Units,
            entryPrice = t.Price,
            closePrice = t.ClosePrice,
            realizedPL = t.RealizedPL,
            openedAt   = t.OpenedAt,
            closedAt   = t.Timestamp,
        }));
    }

    /// <summary>
    /// Checks the PostgreSQL trade history connection from inside the running service.
    /// Returns reachable status, total row count, and any error message.
    /// Safe to call at any time — read-only, never throws.
    /// </summary>
    [HttpGet("db")]
    public async Task<IActionResult> GetDbStatus(CancellationToken ct)
    {
        var (reachable, rowCount, error) = await tradeHistory.GetStatusAsync(ct);
        return Ok(new
        {
            reachable,
            rowCount,
            error,
            checkedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>Emergency close — closes all units for an instrument immediately.</summary>
    [HttpPost("close/{instrument}")]
    public async Task<IActionResult> ClosePosition(string instrument, CancellationToken ct)
    {
        logger.LogWarning("Manual close triggered for {Instrument}", instrument);
        var result = await broker.ClosePositionAsync(instrument, ct);

        return result.Success
            ? Ok(new { message = result.Message, instrument })
            : BadRequest(new { error = result.Message, instrument });
    }
}

public record SetPauseRequest(bool Paused);
