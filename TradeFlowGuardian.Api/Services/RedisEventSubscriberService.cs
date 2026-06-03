using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using TradeFlowGuardian.Api.Hubs;

namespace TradeFlowGuardian.Api.Services;

/// <summary>
/// Subscribes to the Redis pub/sub channel "tradeflow:events".
/// The Worker publishes a JSON event after every order placement or close;
/// this service forwards it to all connected SignalR clients.
/// </summary>
public class RedisEventSubscriberService(
    IConnectionMultiplexer redis,
    IHubContext<TradingHub> hub,
    ILogger<RedisEventSubscriberService> logger) : BackgroundService
{
    public const string Channel = "tradeflow:events";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = redis.GetSubscriber();
        await sub.SubscribeAsync(RedisChannel.Literal(Channel), async (_, message) =>
        {
            if (message.IsNullOrEmpty) return;
            try
            {
                await hub.Clients.All.SendAsync("event", message.ToString(), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to forward Redis event to SignalR clients");
            }
        });

        logger.LogInformation("Redis event subscriber listening on channel {Channel}", Channel);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            await sub.UnsubscribeAsync(RedisChannel.Literal(Channel));
        }
    }
}
