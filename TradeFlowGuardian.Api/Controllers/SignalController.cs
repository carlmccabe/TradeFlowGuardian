using Microsoft.AspNetCore.Mvc;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SignalController(ISignalQueue queue, ILogger<SignalController> logger) : ControllerBase
{
    /// <summary>
    /// Receives TradingView webhook alert and queues it for execution.
    ///
    /// TV Alert Message JSON template:
    /// {
    ///   "instrument": "USD_JPY",
    ///   "direction": "Long",
    ///   "atr": {{plot("ATR")}},
    ///   "price": {{close}},
    ///   "riskPercent": 0,
    ///   "timestamp": "{{timenow}}",
    ///   "idempotencyKey": "{{exchange}}_{{ticker}}_{{time}}"
    /// }
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceiveSignal(
        [FromBody] TradeSignal signal,
        CancellationToken ct)
    {
        logger.LogInformation(
            "Signal received: {Direction} {Instrument} @ {Price} | ATR={Atr} | Key={Key}",
            signal.Direction, signal.Instrument, signal.Price, signal.Atr, signal.IdempotencyKey);

        await queue.EnqueueAsync(signal, ct);

        return Accepted(new
        {
            message = "Signal queued",
            instrument = signal.Instrument,
            direction = signal.Direction.ToString(),
            queuedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>Health check for the signal endpoint.</summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() => Ok(new { status = "ok", utc = DateTimeOffset.UtcNow });
}
