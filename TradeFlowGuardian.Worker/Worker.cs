using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Worker.Handlers;

namespace TradeFlowGuardian.Worker;

/// <summary>
/// Long-running background service that drains the signal queue
/// and dispatches each signal to SignalExecutionHandler.
/// Runs continuously — one signal processed at a time (no parallel execution).
/// </summary>
public class ExecutionWorker : BackgroundService
{
    private readonly ISignalQueue _queue;
    private readonly IServiceProvider _services;
    private readonly ILogger<ExecutionWorker> _logger;

    public ExecutionWorker(
        ISignalQueue queue,
        IServiceProvider services,
        ILogger<ExecutionWorker> logger)
    {
        _queue = queue;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExecutionWorker started — waiting for signals");

        while (!stoppingToken.IsCancellationRequested)
        {
            var signal = await _queue.DequeueAsync(stoppingToken);

            if (signal is null)
                continue;

            // New DI scope per signal so scoped services (filters, sizer) are fresh
            await using var scope = _services.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<SignalExecutionHandler>();

            try
            {
                await handler.HandleAsync(signal, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled exception processing signal {Direction} {Instrument}",
                    signal.Direction, signal.Instrument);
                // Continue — don't crash the worker on a bad signal
            }
        }

        _logger.LogInformation("ExecutionWorker stopped");
    }
}
