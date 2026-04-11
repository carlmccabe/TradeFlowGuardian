using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;

namespace TradeFlowGuardian.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController(
    IOandaClient oanda,
    IPauseState pauseState,
    IDailyDrawdownGuard drawdownGuard,
    IOptions<RiskConfig> risk,
    ILogger<StatusController> logger) : ControllerBase
{
    /// <summary>Returns all instruments with an open position.</summary>
    [HttpGet("positions")]
    public async Task<IActionResult> GetPositions(CancellationToken ct)
    {
        var positions = await oanda.GetAllOpenPositionsAsync(ct);
        return Ok(positions.Select(p => new
        {
            instrument = p.Instrument,
            units = p.Units,
            unrealizedPL = p.UnrealizedPL,
            averagePrice = p.AveragePrice
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
        return Ok(new { paused = body.Paused, updatedAt = DateTimeOffset.UtcNow });
    }

    /// <summary>Returns live account balance from OANDA.</summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance(CancellationToken ct)
    {
        var balance = await oanda.GetAccountBalanceAsync(ct);
        return Ok(new { balanceAud = balance, fetchedAt = DateTimeOffset.UtcNow });
    }

    /// <summary>Returns open position units for a given instrument.</summary>
    [HttpGet("position/{instrument}")]
    public async Task<IActionResult> GetPosition(string instrument, CancellationToken ct)
    {
        var units = await oanda.GetOpenPositionUnitsAsync(instrument, ct);
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
            currentBalance = await oanda.GetAccountBalanceAsync(ct);
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

    /// <summary>Emergency close — closes all units for an instrument immediately.</summary>
    [HttpPost("close/{instrument}")]
    public async Task<IActionResult> ClosePosition(string instrument, CancellationToken ct)
    {
        logger.LogWarning("Manual close triggered for {Instrument}", instrument);
        var result = await oanda.ClosePositionAsync(instrument, ct);

        return result.Success
            ? Ok(new { message = result.Message, instrument })
            : BadRequest(new { error = result.Message, instrument });
    }
}

public record SetPauseRequest(bool Paused);
