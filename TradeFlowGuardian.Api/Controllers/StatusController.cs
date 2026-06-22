using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TradeFlowGuardian.Api.Hubs;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Brokers;
using TradeFlowGuardian.Core.Interfaces;

namespace TradeFlowGuardian.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class StatusController(
    IBrokerClient broker,
    IPauseState pauseState,
    IDailyDrawdownGuard drawdownGuard,
    ITradeHistoryRepository tradeHistory,
    IOptions<RiskConfig> risk,
    IHubContext<TradingHub> hub,
    ILogger<StatusController> logger) : ControllerBase
{
    /// <summary>
    /// Combined status: live balance + all open positions with unrealised P&amp;L.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var balance   = await broker.GetAccountBalanceAsync(ct);
        var positions = await broker.GetAllOpenPositionsAsync(ct);
        return Ok(new
        {
            balanceAud = balance,
            positions  = positions.Select(p => new
            {
                instrument   = p.Instrument,
                units        = p.Units,
                unrealizedPL = p.UnrealizedPL,
                averagePrice = p.AveragePrice,
                side         = p.Units switch { > 0 => "LONG", < 0 => "SHORT", _ => "FLAT" }
            }),
            fetchedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Returns last 90 days of entry+close trade pairs from PostgreSQL.
    /// ExitPrice and ClosedAt are null for still-open trades.
    /// P&L figures are in quote currency (no AUD conversion applied here).
    /// </summary>
    [HttpGet("trades")]
    public async Task<IActionResult> GetTrades(CancellationToken ct)
    {
        var trades = await tradeHistory.GetPairedTradesAsync(90, ct);
        return Ok(trades.Select(t => new
        {
            instrument      = t.Instrument,
            direction       = t.Direction,
            entryPrice      = t.EntryPrice,
            exitPrice       = t.ExitPrice,
            units           = t.Units,
            openedAt        = t.OpenedAt,
            closedAt        = t.ClosedAt,
            durationSeconds = t.DurationSeconds
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
    /// Realized P&amp;L grouped by UTC day (range=daily, 30 days) or ISO week (range=weekly, 13 weeks).
    /// Each bucket includes only fully-closed trades (entry fill paired with a Close fill).
    /// Returns an empty array when there is no matching trade history — never an error.
    /// </summary>
    [HttpGet("pnl")]
    public async Task<IActionResult> GetDailyPnl([FromQuery] string range = "daily", CancellationToken ct = default)
    {
        var weekly = "weekly".Equals(range, StringComparison.OrdinalIgnoreCase);
        var data   = await tradeHistory.GetDailyPnlAsync(weekly, ct);
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
