using Microsoft.AspNetCore.SignalR;

namespace TradeFlowGuardian.Api.Hubs;

/// <summary>
/// SignalR hub for real-time trade and risk-settings push to the dashboard.
/// The server only pushes — clients don't send messages here.
/// Events are broadcast via IHubContext&lt;TradingHub&gt; from:
///   - RedisEventSubscriberService  (Worker → Redis pub/sub → API → SignalR)
///   - RiskController               (API direct push on risk settings change)
/// </summary>
public class TradingHub : Hub { }
