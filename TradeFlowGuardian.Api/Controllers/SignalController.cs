using Microsoft.AspNetCore.Mvc;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SignalController : ControllerBase
{
    private readonly ISignalQueue _queue;
    private readonly ILogger<SignalController> _logger;

    public SignalController(ISignalQueue queue, ILogger<SignalController> logger)
    {
        _queue = queue;
        _logger = logger;
    }

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
        _logger.LogInformation(
            "Signal received: {Direction} {Instrument} @ {Price} | ATR={Atr} | Key={Key}",
            signal.Direction, signal.Instrument, signal.Price, signal.Atr, signal.IdempotencyKey);

        await _queue.EnqueueAsync(signal, ct);

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
