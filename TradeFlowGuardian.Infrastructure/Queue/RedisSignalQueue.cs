using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Queue;

/// <summary>
/// Redis Streams-backed signal queue.
///
/// How it works:
///   API  → XADD  tradeflow:signals * data {json}
///   Worker → XREADGROUP GROUP workers worker-1 COUNT 1 BLOCK 2000 STREAMS tradeflow:signals >
///   Worker → XACK  tradeflow:signals workers {id}   (after successful processing)
///
/// Key properties:
///   • Messages survive API/Worker restarts — persisted in Redis
///   • Consumer groups ensure each signal is delivered to exactly one Worker replica
///   • Unacked messages (crash mid-processing) can be reclaimed via XPENDING (Phase 3)
///   • ">" in XREADGROUP means "give me messages not yet delivered to anyone in this group"
/// </summary>
public class RedisSignalQueue : ISignalQueue
{
    private readonly IDatabase _db;
    private readonly RedisConfig _config;
    private readonly ILogger<RedisSignalQueue> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Lazy init flag — group is created once on first dequeue
    private bool _groupReady;

    public RedisSignalQueue(
        IConnectionMultiplexer redis,
        IOptions<RedisConfig> config,
        ILogger<RedisSignalQueue> logger)
    {
        _db = redis.GetDatabase();
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Serialize the signal to JSON and append to the Redis Stream.
    /// XADD auto-generates a millisecond-precision message ID (e.g. 1712700000000-0).
    /// </summary>
    public async Task EnqueueAsync(TradeSignal signal, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(signal, JsonOpts);

        var id = await _db.StreamAddAsync(
            _config.StreamName,
            "data",
            json);

        _logger.LogInformation(
            "Signal enqueued to Redis Stream | ID={MessageId} | {Direction} {Instrument}",
            id, signal.Direction, signal.Instrument);
    }

    /// <summary>
    /// Blocks until a signal is available in the consumer group, then acks it.
    /// Uses a poll loop (250 ms) because StackExchange.Redis wraps XREADGROUP
    /// without a blocking timeout parameter at this level.
    ///
    /// Delivery guarantee: at-most-once (ack before handler runs).
    /// Phase 3: move ack to after HandleAsync succeeds for at-least-once.
    /// </summary>
    public async Task<TradeSignal?> DequeueAsync(CancellationToken ct = default)
    {
        await EnsureConsumerGroupAsync();

        while (!ct.IsCancellationRequested)
        {
            var entries = await _db.StreamReadGroupAsync(
                _config.StreamName,
                _config.ConsumerGroup,
                _config.ConsumerName,
                ">",          // only messages not yet delivered to any consumer
                count: 1);

            if (entries is { Length: > 0 })
            {
                var entry = entries[0];
                var json   = entry["data"];

                if (json.IsNullOrEmpty)
                {
                    _logger.LogWarning("Redis Stream entry {Id} had no 'data' field — skipping", entry.Id);
                    await _db.StreamAcknowledgeAsync(_config.StreamName, _config.ConsumerGroup, entry.Id);
                    continue;
                }

                var signal = JsonSerializer.Deserialize<TradeSignal>((string)json!, JsonOpts);
                if (signal is null)
                {
                    _logger.LogWarning("Failed to deserialize signal from entry {Id} — skipping", entry.Id);
                    await _db.StreamAcknowledgeAsync(_config.StreamName, _config.ConsumerGroup, entry.Id);
                    continue;
                }

                // Ack immediately — message removed from PEL (Pending Entries List)
                await _db.StreamAcknowledgeAsync(_config.StreamName, _config.ConsumerGroup, entry.Id);

                _logger.LogInformation(
                    "Signal dequeued from Redis Stream | ID={MessageId} | {Direction} {Instrument}",
                    entry.Id, signal.Direction, signal.Instrument);

                return signal;
            }

            // No messages — yield briefly before polling again
            await Task.Delay(250, ct);
        }

        return null;
    }

    /// <summary>
    /// Creates the consumer group and stream (if missing) on first use.
    /// BUSYGROUP error means the group already exists — safe to ignore on restart.
    /// </summary>
    private async Task EnsureConsumerGroupAsync()
    {
        if (_groupReady) return;

        try
        {
            // 0 = deliver all messages from the beginning of the stream.
            // On restart the group already exists (BUSYGROUP) so this path is skipped
            // and Redis resumes from the last-delivered-id automatically.
            // createStream: true = MKSTREAM flag — creates stream if it doesn't exist
            await _db.StreamCreateConsumerGroupAsync(
                _config.StreamName,
                _config.ConsumerGroup,
                StreamPosition.Beginning,
                createStream: true);

            _logger.LogInformation(
                "Redis consumer group '{Group}' created on stream '{Stream}'",
                _config.ConsumerGroup, _config.StreamName);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP"))
        {
            // Group already exists — normal on Worker restart
            _logger.LogDebug(
                "Consumer group '{Group}' already exists on stream '{Stream}' — reusing",
                _config.ConsumerGroup, _config.StreamName);
        }

        _groupReady = true;
    }
}
