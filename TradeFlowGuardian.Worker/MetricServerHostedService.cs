using Prometheus;

namespace TradeFlowGuardian.Worker;

/// <summary>
/// Wraps KestrelMetricServer in an IHostedService so its lifetime is
/// tied to the Worker host. KestrelMetricServer is not itself an IHostedService,
/// so this bridge is required for clean startup/shutdown.
///
/// Exposes /metrics on port 9091 — scraped by Prometheus in docker-compose.
/// </summary>
public sealed class MetricServerHostedService(KestrelMetricServer server) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        server.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => server.StopAsync();
}
