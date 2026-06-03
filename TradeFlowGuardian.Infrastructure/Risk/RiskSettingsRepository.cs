using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using TradeFlowGuardian.Infrastructure.Data;

namespace TradeFlowGuardian.Infrastructure.Risk;

public class RiskSettingsRepository(
    TradeFlowDbContext db,
    ILogger<RiskSettingsRepository> logger) : IRiskSettingsRepository
{
    public async Task<RiskSettings?> GetByInstrumentAsync(string instrument, CancellationToken ct = default)
    {
        try
        {
            return await db.RiskSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Instrument == instrument, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read risk settings for {Instrument}", instrument);
            return null;
        }
    }

    public async Task<IReadOnlyList<RiskSettings>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            return await db.RiskSettings
                .AsNoTracking()
                .OrderBy(r => r.Instrument)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read all risk settings");
            return [];
        }
    }

    public async Task<RiskSettings> UpsertAsync(
        string instrument, decimal? riskPercent, bool? isActive, CancellationToken ct = default)
    {
        var existing = await db.RiskSettings.FirstOrDefaultAsync(r => r.Instrument == instrument, ct);
        if (existing is null)
        {
            existing = new RiskSettings { Instrument = instrument };
            db.RiskSettings.Add(existing);
        }

        if (riskPercent.HasValue) existing.RiskPercent = riskPercent.Value;
        if (isActive.HasValue)    existing.IsActive    = isActive.Value;
        existing.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task SetAllActiveAsync(bool isActive, CancellationToken ct = default)
    {
        await db.RiskSettings
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsActive,   isActive)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow),
                ct);
    }
}
