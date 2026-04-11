using Microsoft.AspNetCore.Mvc;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PriceController(IOandaClient oanda, ILogger<PriceController> logger) : ControllerBase
{
    /// <summary>Returns the current mid price for an instrument.</summary>
    [HttpGet("mid/{instrument}")]
    public async Task<IActionResult> GetMidPrice(string instrument, CancellationToken ct)
    {
        var mid = await oanda.GetMidPriceAsync(instrument, ct);
        logger.LogInformation("Mid price for {Instrument} is {MidPrice}", instrument, mid);
        
        if (!mid.HasValue)
            logger.LogWarning("Failed to fetch mid price for instrument {Instrument}", instrument);
        
        return mid.HasValue
            ? Ok(new { instrument, mid, fetchedAt = DateTimeOffset.UtcNow })
            : StatusCode(502, new { error = "Pricing unavailable", instrument });
    }

    /// <summary>Returns a full price snapshot (Bid, Ask, Mid, Spread) for an instrument.</summary>
    [HttpGet("snapshot/{instrument}")]
    [ProducesResponseType(typeof(PriceSnapshot), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPriceSnapshot(string instrument, CancellationToken ct)
    {
        var snapshot = await oanda.GetPriceSnapshotAsync(instrument, ct);
        
        if (snapshot is null)
        {
            logger.LogWarning("Failed to fetch price snapshot for instrument {Instrument}", instrument);
            return StatusCode(502, new { error = "Pricing unavailable", instrument });
        }

        return Ok(snapshot);
    }

    /// <summary>Returns only the current bid price for an instrument.</summary>
    [HttpGet("bid/{instrument}")]
    public async Task<IActionResult> GetBidPrice(string instrument, CancellationToken ct)
    {
        var snapshot = await oanda.GetPriceSnapshotAsync(instrument, ct);
        return snapshot is not null
            ? Ok(new { instrument, bid = snapshot.Bid, fetchedAt = snapshot.FetchedAt })
            : StatusCode(502, new { error = "Pricing unavailable", instrument });
    }

    /// <summary>Returns only the current ask price for an instrument.</summary>
    [HttpGet("ask/{instrument}")]
    public async Task<IActionResult> GetAskPrice(string instrument, CancellationToken ct)
    {
        var snapshot = await oanda.GetPriceSnapshotAsync(instrument, ct);
        return snapshot is not null
            ? Ok(new { instrument, ask = snapshot.Ask, fetchedAt = snapshot.FetchedAt })
            : StatusCode(502, new { error = "Pricing unavailable", instrument });
    }

    /// <summary>Legacy endpoint for mid price (mapped to /api/price/price/{instrument}).</summary>
    [HttpGet("price/{instrument}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public Task<IActionResult> GetPriceLegacy(string instrument, CancellationToken ct) => GetMidPrice(instrument, ct);
}