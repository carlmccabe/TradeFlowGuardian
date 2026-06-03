using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Risk;

/// <summary>
/// Used when Postgres is not configured. Returns null for all lookups so callers
/// fall through to DefaultRiskPercent and treat every instrument as active.
/// </summary>
public class NoOpRiskSettingsRepository : IRiskSettingsRepository
{
    public Task<RiskSettings?> GetByInstrumentAsync(string instrument, CancellationToken ct = default)
        => Task.FromResult<RiskSettings?>(null);

    public Task<IReadOnlyList<RiskSettings>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RiskSettings>>([]);

    public Task<RiskSettings> UpsertAsync(string instrument, decimal? riskPercent, bool? isActive, CancellationToken ct = default)
        => Task.FromResult(new RiskSettings { Instrument = instrument });

    public Task SetAllActiveAsync(bool isActive, CancellationToken ct = default)
        => Task.CompletedTask;
}
