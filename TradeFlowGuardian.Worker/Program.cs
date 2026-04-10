using Prometheus;
using StackExchange.Redis;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Infrastructure.Filters;
using TradeFlowGuardian.Infrastructure.Oanda;
using TradeFlowGuardian.Infrastructure.Queue;
using TradeFlowGuardian.Worker;
using TradeFlowGuardian.Worker.Handlers;

var builder = Host.CreateApplicationBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<OandaConfig>(builder.Configuration.GetSection("Oanda"));
builder.Services.Configure<RiskConfig>(builder.Configuration.GetSection("Risk"));
builder.Services.Configure<FilterConfig>(builder.Configuration.GetSection("Filters"));
builder.Services.Configure<RedisConfig>(builder.Configuration.GetSection("Redis"));

// ── Infrastructure ────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<IOandaClient, OandaClient>();

// ── Redis ─────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379"));

builder.Services.AddSingleton<ISignalQueue, RedisSignalQueue>();
builder.Services.AddScoped<IPositionSizer, PositionSizer>();

// ── Filters ───────────────────────────────────────────────────────────────────
builder.Services.AddScoped<SignalAgeFilter>();
builder.Services.AddScoped<AtrSpikeFilter>();
builder.Services.AddScoped<ISignalFilter, CompositeSignalFilter>(sp =>
    new CompositeSignalFilter(new List<ISignalFilter>
    {
        sp.GetRequiredService<SignalAgeFilter>(),
        sp.GetRequiredService<AtrSpikeFilter>()
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
