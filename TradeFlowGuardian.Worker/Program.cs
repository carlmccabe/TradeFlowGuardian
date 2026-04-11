using Prometheus;
using StackExchange.Redis;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Infrastructure.Calendar;
using TradeFlowGuardian.Infrastructure.Filters;
using TradeFlowGuardian.Infrastructure.Oanda;
using TradeFlowGuardian.Infrastructure.Cache;
using TradeFlowGuardian.Infrastructure.Drawdown;
using TradeFlowGuardian.Infrastructure.Queue;
using TradeFlowGuardian.Worker;
using TradeFlowGuardian.Worker.Handlers;

var builder = Host.CreateApplicationBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<OandaConfig>(builder.Configuration.GetSection("Oanda"));
builder.Services.Configure<RiskConfig>(builder.Configuration.GetSection("Risk"));
builder.Services.Configure<FilterConfig>(builder.Configuration.GetSection("Filters"));
builder.Services.Configure<RedisConfig>(builder.Configuration.GetSection("Redis"));
builder.Services.Configure<NewsFilterOptions>(builder.Configuration.GetSection("NewsFilter"));

// ── Infrastructure ────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<IOandaClient, OandaClient>();
builder.Services.AddHttpClient(ForexFactoryCalendarService.HttpClientName, client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TradeFlowGuardian/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ── Redis ─────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379"));

builder.Services.AddSingleton<ISignalQueue, RedisSignalQueue>();
builder.Services.AddSingleton<IPositionCache, RedisPositionCache>();
builder.Services.AddSingleton<IDailyDrawdownGuard, DailyDrawdownGuard>();
builder.Services.AddScoped<IPositionSizer, PositionSizer>();

// ── Filters ───────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IEconomicCalendarService, ForexFactoryCalendarService>();
builder.Services.AddScoped<SignalAgeFilter>();
builder.Services.AddScoped<DailyDrawdownFilter>();
builder.Services.AddScoped<AtrSpikeFilter>();
builder.Services.AddScoped<NewsCalendarFilter>();
builder.Services.AddScoped<ISignalFilter, CompositeSignalFilter>(sp =>
    new CompositeSignalFilter(new List<ISignalFilter>
    {
        sp.GetRequiredService<SignalAgeFilter>(),
        sp.GetRequiredService<DailyDrawdownFilter>(),
        sp.GetRequiredService<AtrSpikeFilter>(),
        sp.GetRequiredService<NewsCalendarFilter>()
    }));

// ── Worker ────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<SignalExecutionHandler>();
builder.Services.AddHostedService<ExecutionWorker>();

// ── Metrics (Prometheus) ──────────────────────────────────────────────────────
// Exposes /metrics on port 9091 — scraped by Prometheus in docker-compose
builder.Services.AddSingleton(_ => new KestrelMetricServer(port: 9091));
builder.Services.AddHostedService<MetricServerHostedService>();

var host = builder.Build();
host.Run();
