using Microsoft.AspNetCore.Mvc;
using TradeFlowGuardian.Core.Interfaces;

namespace TradeFlowGuardian.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly IOandaClient _oanda;
    private readonly IFilterStateService _filterState;
    private readonly ILogger<StatusController> _logger;

    public StatusController(
        IOandaClient oanda,
        IFilterStateService filterState,
        ILogger<StatusController> logger)
    {
        _oanda = oanda;
        _filterState = filterState;
        _logger = logger;
    }

    /// <summary>Returns live account balance from OANDA.</summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance(CancellationToken ct)
    {
        var balance = await _oanda.GetAccountBalanceAsync(ct);
        return Ok(new { balanceAud = balance, fetchedAt = DateTimeOffset.UtcNow });
    }

    /// <summary>Returns open position units for a given instrument.</summary>
    [HttpGet("position/{instrument}")]
    public async Task<IActionResult> GetPosition(string instrument, CancellationToken ct)
    {
        var units = await _oanda.GetOpenPositionUnitsAsync(instrument, ct);
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

    /// <summary>Emergency close — closes all units for an instrument immediately.</summary>
    [HttpPost("close/{instrument}")]
    public async Task<IActionResult> ClosePosition(string instrument, CancellationToken ct)
    {
        _logger.LogWarning("Manual close triggered for {Instrument}", instrument);
        var result = await _oanda.ClosePositionAsync(instrument, ct);

        return result.Success
            ? Ok(new { message = result.Message, instrument })
            : BadRequest(new { error = result.Message, instrument });
    }

    /// <summary>
    /// Returns the current state of all signal filters.
    /// atrSpike and newsBlocked reflect the outcome of the most recent signal evaluation
    /// by the respective filter; paused reflects the operator-controlled pause flag.
    /// </summary>
    [HttpGet("filters")]
    public async Task<IActionResult> GetFilterStatus(CancellationToken ct)
    {
        var paused      = await _filterState.GetPausedAsync(ct);
        var atrSpike    = await _filterState.GetAtrSpikeAsync(ct);
        var newsBlocked = await _filterState.GetNewsBlockedAsync(ct);

        return Ok(new
        {
            paused,
            atrSpike,
            newsBlocked,
            fetchedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Pauses or resumes signal execution.
    /// When paused, PauseFilter blocks every incoming signal before any other filter runs.
    /// </summary>
    [HttpPost("pause")]
    public async Task<IActionResult> SetPaused([FromBody] SetPausedRequest body, CancellationToken ct)
    {
        await _filterState.SetPausedAsync(body.Paused, ct);

        _logger.LogWarning("Trading {State} by operator", body.Paused ? "PAUSED" : "RESUMED");

        return Ok(new
        {
            paused = body.Paused,
            message = body.Paused ? "Trading paused" : "Trading resumed",
            updatedAt = DateTimeOffset.UtcNow
        });
    }
}

public record SetPausedRequest(bool Paused);
