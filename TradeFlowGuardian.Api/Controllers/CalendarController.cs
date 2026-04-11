using Microsoft.AspNetCore.Mvc;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalendarController(IEconomicCalendarService calendarService, ILogger<CalendarController> logger) : ControllerBase
{
    /// <summary>
    /// Returns upcoming economic events for a given instrument (e.g. "EUR_USD").
    /// Splits the instrument into its constituent currencies and queries the calendar.
    /// </summary>
    /// <param name="instrument">The OANDA-style instrument name, e.g. "EUR_USD".</param>
    /// <param name="lookaheadHours">How many hours into the future/past to look. Defaults to 24.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("events/{instrument}")]
    [ProducesResponseType(typeof(IEnumerable<EconomicEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEvents(string instrument, [FromQuery] int lookaheadHours = 24, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(instrument))
        {
            return BadRequest(new { error = "Instrument is required." });
        }

        // Handle both "EUR_USD" and "EURUSD" if possible, but OANDA uses "_"
        var currencies = instrument.Split(['_', '/', '-'], StringSplitOptions.RemoveEmptyEntries);

        if (currencies.Length == 0)
        {
            return BadRequest(new { error = $"Invalid instrument format: {instrument}" });
        }

        logger.LogInformation("Fetching calendar events for {Instrument} (Currencies: {Currencies}) with {Lookahead}h lookahead", 
            instrument, string.Join(", ", currencies), lookaheadHours);

        var events = await calendarService.GetUpcomingEventsAsync(
            currencies, 
            TimeSpan.FromHours(lookaheadHours), 
            ct);

        return Ok(events);
    }
}
