using System.Threading.Channels;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Queue;

/// <summary>
/// In-memory bounded channel queue.
/// API enqueues signals; Worker dequeues and executes.
/// Phase 2: replace with Redis Streams for persistence across restarts.
/// </summary>
public class InMemorySignalQueue : ISignalQueue
{
    private readonly Channel<TradeSignal> _channel = Channel.CreateBounded<TradeSignal>(new BoundedChannelOptions(50)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false
    });

    public async Task EnqueueAsync(TradeSignal signal, CancellationToken ct = default)
        => await _channel.Writer.WriteAsync(signal, ct);

    public async Task<TradeSignal?> DequeueAsync(CancellationToken ct = default)
    {
        try
        {
            return await _channel.Reader.ReadAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
