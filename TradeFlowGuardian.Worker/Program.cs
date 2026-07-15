using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prometheus;
using StackExchange.Redis;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Brokers;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Infrastructure.Accounts;
using TradeFlowGuardian.Infrastructure.Calendar;
using TradeFlowGuardian.Infrastructure.Data;
using TradeFlowGuardian.Infrastructure.Filters;
using TradeFlowGuardian.Infrastructure.Brokers.Oanda;
using TradeFlowGuardian.Infrastructure.Sizing;
using TradeFlowGuardian.Infrastructure.Cache;
using TradeFlowGuardian.Infrastructure.Drawdown;
using TradeFlowGuardian.Infrastructure.History;
using TradeFlowGuardian.Infrastructure.Logging;
using TradeFlowGuardian.Infrastructure.Pause;
using TradeFlowGuardian.Infrastructure.Queue;
using TradeFlowGuardian.Infrastructure.Risk;
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
    // JSON console: Railway parses structured fields, exceptions land in full with
    // stack traces, and each entry stays one line in the log stream.
    builder.Logging.AddJsonConsole(opts =>
    {
        opts.IncludeScopes = true;
        opts.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        opts.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
    });
}
// Ships logs to Grafana Cloud (Loki) when OTEL_EXPORTER_OTLP_* env vars are set;
// no-op otherwise. Console logging above is unaffected. See docs/LOGGING.md.
builder.Logging.AddOtlpExportIfConfigured("tradeflow-worker");

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<OandaConfig>(builder.Configuration.GetSection("Oanda"));
builder.Services.Configure<RiskConfig>(builder.Configuration.GetSection("Risk"));
builder.Services.Configure<FilterConfig>(builder.Configuration.GetSection("Filters"));
builder.Services.Configure<RedisConfig>(builder.Configuration.GetSection("Redis"));
builder.Services.Configure<NewsFilterOptions>(builder.Configuration.GetSection("NewsFilter"));
builder.Services.Configure<PostgresConfig>(builder.Configuration.GetSection("Postgres"));

// ── Infrastructure ────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<IBrokerClient, OandaBrokerClient>();
builder.Services.AddHttpClient(ForexFactoryCalendarService.HttpClientName, client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TradeFlowGuardian/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ── Redis ─────────────────────────────────────────────────────────────────────
// Connected eagerly (AbortOnConnectFail=false, so this never throws) because
// Data Protection key persistence needs the multiplexer instance at registration.
var redisMux = ConnectionMultiplexer.Connect(
    ParseRedisOptions(builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379"));
builder.Services.AddSingleton<IConnectionMultiplexer>(redisMux);

builder.Services.AddSingleton<ISignalQueue, RedisSignalQueue>();
builder.Services.AddSingleton<IPositionCache, RedisPositionCache>();
builder.Services.AddSingleton<IPauseState, RedisPauseState>();
builder.Services.AddSingleton<IDailyDrawdownGuard, DailyDrawdownGuard>();
builder.Services.AddScoped<IPositionSizer, PositionSizer>();
builder.Services.AddScoped<ITradeHistoryRepository, TradeHistoryRepository>();

// ── Risk settings (EF / PostgreSQL) ──────────────────────────────────────────
{
    var pgCs = PostgresConnectionHelper.Normalize(builder.Configuration["Postgres:ConnectionString"]);
    if (!string.IsNullOrWhiteSpace(pgCs))
    {
        builder.Services.AddDbContext<TradeFlowDbContext>(opts =>
            opts.UseNpgsql(pgCs));
        builder.Services.AddScoped<IRiskSettingsRepository, RiskSettingsRepository>();
    }
    else
    {
        // No Postgres — register a no-op so SignalExecutionHandler still resolves.
        // All signals will be allowed (IsActive defaults to true when settings are absent).
        builder.Services.AddScoped<IRiskSettingsRepository, NoOpRiskSettingsRepository>();
    }

    // ── Account registry ──────────────────────────────────────────────────────
    // Same registry as the Api — guarantees both services trade the same account.
    builder.Services.AddAccountManagement(redisMux, hasPostgres: !string.IsNullOrWhiteSpace(pgCs));
}

// ── Filters ───────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IEconomicCalendarService, ForexFactoryCalendarService>();
builder.Services.AddScoped<SignalAgeFilter>();
builder.Services.AddScoped<GlobalPauseFilter>();
builder.Services.AddScoped<DailyDrawdownFilter>();
builder.Services.AddScoped<AtrSpikeFilter>();
builder.Services.AddScoped<NewsCalendarFilter>();
builder.Services.AddScoped<ISignalFilter, CompositeSignalFilter>(sp =>
    new CompositeSignalFilter(new List<ISignalFilter>
    {
        sp.GetRequiredService<SignalAgeFilter>(),
        sp.GetRequiredService<GlobalPauseFilter>(),
        sp.GetRequiredService<DailyDrawdownFilter>(),
        sp.GetRequiredService<AtrSpikeFilter>(),
        sp.GetRequiredService<NewsCalendarFilter>()
    }));

// ── Worker ────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<SignalExecutionHandler>();
builder.Services.AddHostedService<ExecutionWorker>();
builder.Services.AddHostedService<TradeFlowGuardian.Worker.Services.WorkerHeartbeatService>();

// ── Shutdown ──────────────────────────────────────────────────────────────────
// Railway sends SIGTERM and waits 30 s before SIGKILL. Give in-flight order
// calls (which use CancellationToken.None) time to complete before the host
// forces shutdown. 5 s (the .NET default) is not enough.
builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));

// ── Metrics (Prometheus) ──────────────────────────────────────────────────────
// Local docker-compose: port 9091. Railway: honours injected PORT env var so
// the health check at /metrics succeeds on whatever port Railway assigns.
var metricsPort = int.TryParse(builder.Configuration["PORT"], out var p) ? p : 9091;
builder.Services.AddSingleton(_ => new KestrelMetricServer(port: metricsPort));
builder.Services.AddHostedService<MetricServerHostedService>();

var host = builder.Build();

// ── Startup config banner ─────────────────────────────────────────────────────
{
    var startupLog    = host.Services.GetRequiredService<ILogger<Program>>();
    var accountProvider = host.Services.GetRequiredService<IActiveAccountProvider>();
    var redisCfg      = host.Services.GetRequiredService<IOptions<RedisConfig>>().Value;
    var filterCfg     = host.Services.GetRequiredService<IOptions<FilterConfig>>().Value;
    var newsCfg       = host.Services.GetRequiredService<IOptions<NewsFilterOptions>>().Value;
    try
    {
        var activeAcct = await accountProvider.GetActiveAsync();
        startupLog.LogInformation(
            "Worker starting | OANDA={OandaEnv} | Account={Label} ({AccountId}) | Url={BaseUrl} | Redis={Redis} | Stream={Stream} | Consumer={Consumer} | AtrFilter={AtrFilter} | NewsFilter={NewsFilter}",
            activeAcct.Environment, activeAcct.Label, activeAcct.AccountId, activeAcct.BaseUrl,
            RedisHost(redisCfg.ConnectionString), redisCfg.StreamName, redisCfg.ConsumerName,
            filterCfg.EnableAtrSpikeFilter, newsCfg.Enabled);
    }
    catch (Exception ex)
    {
        startupLog.LogCritical(ex,
            "Worker starting but NO active OANDA account found — trades will fail until an account is activated via /api/accounts");
    }

    // ── Postgres probe ────────────────────────────────────────────────────────
    {
        await using var scope = host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITradeHistoryRepository>();
        var (reachable, rowCount, error) = await repo.GetStatusAsync(CancellationToken.None);
        if (reachable)
            startupLog.LogInformation("Postgres: connected | trade_history rows={RowCount}", rowCount);
        else
            startupLog.LogWarning("Postgres: unreachable or not configured | {Error}",
                error ?? "connection string empty");
    }
}

host.Run();

// Strips auth credentials from both redis://user:pass@host and host:port formats.
static string RedisHost(string cs) =>
    System.Text.RegularExpressions.Regex.Replace(cs, @"redis://[^@]+@", "").Split(',')[0];

// StackExchange.Redis does not understand redis:// / rediss:// URI format.
// Railway (and many other providers) supply the connection string in that form,
// so we parse it manually and build ConfigurationOptions from the URI components.
// Plain host:port[,options] strings are forwarded to ConfigurationOptions.Parse().
static ConfigurationOptions ParseRedisOptions(string cs)
{
    if (cs.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) ||
        cs.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(cs);
        var opts = new ConfigurationOptions
        {
            Ssl = cs.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase),
            AbortOnConnectFail = false,
        };
        opts.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            if (parts.Length == 2 && !string.IsNullOrEmpty(parts[1]))
                opts.Password = Uri.UnescapeDataString(parts[1]);
        }
        return opts;
    }

    var options = ConfigurationOptions.Parse(cs);
    options.AbortOnConnectFail = false;
    return options;
}
