using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using TradeFlowGuardian.Api.Hubs;
using TradeFlowGuardian.Core.Interfaces;

namespace TradeFlowGuardian.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class RiskController(
    IRiskSettingsRepository riskRepo,
    IHubContext<TradingHub> hub,
    ILogger<RiskController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var settings = await riskRepo.GetAllAsync(ct);
        return Ok(settings.Select(Project));
    }

    [HttpPatch("{instrument}")]
    public async Task<IActionResult> Update(
        string instrument,
        [FromBody] UpdateRiskRequest body,
        CancellationToken ct)
    {
        if (body.RiskPercent.HasValue && (body.RiskPercent < 0.5m || body.RiskPercent > 3.0m))
            return BadRequest(new { error = "riskPercent must be between 0.5 and 3.0" });

        var updated = await riskRepo.UpsertAsync(instrument, body.RiskPercent, body.IsActive, ct);

        await BroadcastRiskUpdateAsync(updated.Instrument, updated.RiskPercent, updated.IsActive, ct);
        logger.LogInformation("Risk settings updated for {Instrument}: risk={Risk}% active={Active}",
            instrument, updated.RiskPercent, updated.IsActive);

        return Ok(Project(updated));
    }

    [HttpPost("pause-all")]
    public async Task<IActionResult> PauseAll(CancellationToken ct)
    {
        await riskRepo.SetAllActiveAsync(false, ct);
        await BroadcastBulkUpdateAsync(false, ct);
        logger.LogWarning("All instruments paused via /risk/pause-all");
        return Ok(new { paused = true, updatedAt = DateTimeOffset.UtcNow });
    }

    [HttpPost("resume-all")]
    public async Task<IActionResult> ResumeAll(CancellationToken ct)
    {
        await riskRepo.SetAllActiveAsync(true, ct);
        await BroadcastBulkUpdateAsync(true, ct);
        logger.LogInformation("All instruments resumed via /risk/resume-all");
        return Ok(new { resumed = true, updatedAt = DateTimeOffset.UtcNow });
    }

    private async Task BroadcastRiskUpdateAsync(
        string instrument, decimal riskPercent, bool isActive, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = "risk_updated",
            instrument,
            riskPercent,
            isActive
        });
        await hub.Clients.All.SendAsync("event", payload, ct);
    }

    private async Task BroadcastBulkUpdateAsync(bool isActive, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = "risk_bulk_updated",
            isActive
        });
        await hub.Clients.All.SendAsync("event", payload, ct);
    }

    private static object Project(TradeFlowGuardian.Core.Models.RiskSettings r) =>
        new { instrument = r.Instrument, riskPercent = r.RiskPercent, isActive = r.IsActive, updatedAt = r.UpdatedAt };
}

public record UpdateRiskRequest(decimal? RiskPercent, bool? IsActive);
