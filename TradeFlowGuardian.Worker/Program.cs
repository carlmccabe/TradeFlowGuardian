using Microsoft.Extensions.Options;
using Prometheus;
using StackExchange.Redis;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Infrastructure.Calendar;
using TradeFlowGuardian.Infrastructure.Filters;
using TradeFlowGuardian.Infrastructure.Oanda;
using TradeFlowGuardian.Infrastructure.Cache;
using TradeFlowGuardian.Infrastructure.Queue;
using TradeFlowGuardian.Worker;
using TradeFlowGuardian.Worker.Handlers;

var builder = Host.CreateApplicationBuilder(args);

// ── Logging ───────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}
else
{
    builder.Logging.AddSimpleConsole(opts =>
    {
        opts.SingleLine = true;
        opts.TimestampFormat = "HH:mm:ss ";
        opts.IncludeScopes = true;
    });
}

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
builder.Services.AddScoped<IPositionSizer, PositionSizer>();

// ── Filters ───────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IEconomicCalendarService, ForexFactoryCalendarService>();
builder.Services.AddScoped<SignalAgeFilter>();
builder.Services.AddScoped<AtrSpikeFilter>();
builder.Services.AddScoped<NewsCalendarFilter>();
builder.Services.AddScoped<ISignalFilter, CompositeSignalFilter>(sp =>
    new CompositeSignalFilter(new List<ISignalFilter>
    {
        sp.GetRequiredService<SignalAgeFilter>(),
        sp.GetRequiredService<AtrSpikeFilter>(),
        sp.GetRequiredService<NewsCalendarFilter>()
    }));

// ── Worker ────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<SignalExecutionHandler>();
builder.Services.AddHostedService<ExecutionWorker>();

// ── Metrics (Prometheus) ──────────────────────────────────────────────────────
// Local docker-compose: port 9091. Railway: honours injected PORT env var so
// the health check at /metrics succeeds on whatever port Railway assigns.
var metricsPort = int.TryParse(builder.Configuration["PORT"], out var p) ? p : 9091;
builder.Services.AddSingleton(_ => new KestrelMetricServer(port: metricsPort));
builder.Services.AddHostedService<MetricServerHostedService>();

var host = builder.Build();

// ── Startup config banner ─────────────────────────────────────────────────────
{
    var startupLog = host.Services.GetRequiredService<ILogger<Program>>();
    var oandaCfg   = host.Services.GetRequiredService<IOptions<OandaConfig>>().Value;
    var redisCfg   = host.Services.GetRequiredService<IOptions<RedisConfig>>().Value;
    var filterCfg  = host.Services.GetRequiredService<IOptions<FilterConfig>>().Value;
    var newsCfg    = host.Services.GetRequiredService<IOptions<NewsFilterOptions>>().Value;
    startupLog.LogInformation(
        "Worker starting | OANDA={OandaEnv} | Url={BaseUrl} | Redis={Redis} | Stream={Stream} | Consumer={Consumer} | AtrFilter={AtrFilter} | NewsFilter={NewsFilter}",
        oandaCfg.Environment, oandaCfg.BaseUrl,
        RedisHost(redisCfg.ConnectionString), redisCfg.StreamName, redisCfg.ConsumerName,
        filterCfg.EnableAtrSpikeFilter, newsCfg.Enabled);
}

host.Run();

// Strips auth credentials from both redis://user:pass@host and host:port formats.
static string RedisHost(string cs) =>
    System.Text.RegularExpressions.Regex.Replace(cs, @"redis://[^@]+@", "").Split(',')[0];
