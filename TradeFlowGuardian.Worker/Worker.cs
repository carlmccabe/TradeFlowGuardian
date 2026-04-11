using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Infrastructure.Observability;
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
    private readonly IDatabase _db;
    private readonly RedisConfig _redisConfig;

    public ExecutionWorker(
        ISignalQueue queue,
        IServiceProvider services,
        ILogger<ExecutionWorker> logger,
        IConnectionMultiplexer redis,
        IOptions<RedisConfig> redisConfig)
    {
        _queue = queue;
        _services = services;
        _logger = logger;
        _db = redis.GetDatabase();
        _redisConfig = redisConfig.Value;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ExecutionWorker shutdown requested — waiting for in-flight signal to complete");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("ExecutionWorker stopped cleanly");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExecutionWorker started — waiting for signals");

        while (!stoppingToken.IsCancellationRequested)
        {
            TradeSignal? signal;
            try
            {
                signal = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("ExecutionWorker idle at shutdown — no signal in flight");
                break;
            }

            if (signal is null)
                continue;

            // Update queue depth gauge — uses XPENDING (actual backlog) not XLEN (total history)
            try
            {
                var pending = await _db.StreamPendingAsync(_redisConfig.StreamName, _redisConfig.ConsumerGroup);
                TradeMetrics.RedisQueueDepth.Set(pending.PendingMessageCount);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not update queue depth metric");
            }

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
    }
}
