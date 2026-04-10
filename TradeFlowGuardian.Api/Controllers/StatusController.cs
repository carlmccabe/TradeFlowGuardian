using Microsoft.AspNetCore.Mvc;
using TradeFlowGuardian.Core.Interfaces;

namespace TradeFlowGuardian.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly IOandaClient _oanda;
    private readonly ILogger<StatusController> _logger;

    public StatusController(IOandaClient oanda, ILogger<StatusController> logger)
    {
        _oanda = oanda;
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
}
