using Microsoft.Extensions.Options;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Filters;

/// <summary>
/// Rejects signals older than FilterConfig.SignalMaxAgeSeconds.
/// Prevents re-execution of delayed or retried TV webhook deliveries.
/// </summary>
public class SignalAgeFilter : ISignalFilter
{
    private readonly FilterConfig _config;

    public SignalAgeFilter(IOptions<FilterConfig> config) => _config = config.Value;

    public Task<FilterResult> EvaluateAsync(TradeSignal signal, CancellationToken ct = default)
    {
        var age = DateTimeOffset.UtcNow - signal.Timestamp;

        return age.TotalSeconds > _config.SignalMaxAgeSeconds
            ? Task.FromResult(FilterResult.Block($"Signal too old: {age.TotalSeconds:F0}s > {_config.SignalMaxAgeSeconds}s limit"))
            : Task.FromResult(FilterResult.Allow());
    }
}
