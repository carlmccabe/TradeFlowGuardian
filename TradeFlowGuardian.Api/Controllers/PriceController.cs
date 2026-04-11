using Microsoft.AspNetCore.Mvc;
using TradeFlowGuardian.Core.Interfaces;

namespace TradeFlowGuardian.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PriceController(IOandaClient oanda, ILogger<PriceController> logger) : ControllerBase
{
    [HttpGet("price/{instrument}")]
    public async Task<IActionResult> GetPrice(string instrument, CancellationToken ct)
    {
        var mid = await oanda.GetMidPriceAsync(instrument, ct);
        logger.LogInformation("Mid price for {Instrument} is {MidPrice}", instrument, mid);
        
        if (!mid.HasValue)
            logger.LogWarning("Failed to fetch mid price for instrument {Instrument}", instrument);
        
        return mid.HasValue
            ? Ok(new { instrument, mid, fetchedAt = DateTimeOffset.UtcNow })
            : StatusCode(502, new { error = "Pricing unavailable", instrument });
    }
}