using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Prometheus;
using StackExchange.Redis;
using TradeFlowGuardian.Api.Middleware;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Infrastructure.Calendar;
using TradeFlowGuardian.Infrastructure.Drawdown;
using TradeFlowGuardian.Infrastructure.Filters;
using TradeFlowGuardian.Infrastructure.History;
using TradeFlowGuardian.Infrastructure.Pause;
using TradeFlowGuardian.Infrastructure.Oanda;
using TradeFlowGuardian.Infrastructure.Queue;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ───────────────────────────────────────────────────────────────────
// Development: default multi-line console with colours.
// Production (Railway): single-line so each entry is one line in the log stream.
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
builder.Services.Configure<WebhookConfig>(builder.Configuration.GetSection("Webhook"));
builder.Services.Configure<RedisConfig>(builder.Configuration.GetSection("Redis"));
builder.Services.Configure<NewsFilterOptions>(builder.Configuration.GetSection("NewsFilter"));
builder.Services.Configure<PostgresConfig>(builder.Configuration.GetSection("Postgres"));

// ── HTTP Client ───────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<IOandaClient, OandaClient>();

// ── Redis ─────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379"));

// ── Core Services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ISignalQueue, RedisSignalQueue>();
builder.Services.AddSingleton<IPauseState, RedisPauseState>();
builder.Services.AddSingleton<IDailyDrawdownGuard, DailyDrawdownGuard>();
builder.Services.AddSingleton<IEconomicCalendarService, ForexFactoryCalendarService>();
builder.Services.AddHttpClient(ForexFactoryCalendarService.HttpClientName, client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TradeFlowGuardian/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IPositionSizer, PositionSizer>();
builder.Services.AddScoped<ITradeHistoryRepository, TradeHistoryRepository>();

// ── Filters (evaluation order — cheapest first) ───────────────────────────────
builder.Services.AddScoped<SignalAgeFilter>();
builder.Services.AddScoped<AtrSpikeFilter>();
builder.Services.AddScoped<ISignalFilter, CompositeSignalFilter>(sp =>
    new CompositeSignalFilter(new List<ISignalFilter>
    {
        sp.GetRequiredService<SignalAgeFilter>(),
        sp.GetRequiredService<AtrSpikeFilter>()
    }));

// ── Shutdown ──────────────────────────────────────────────────────────────────
// Allow in-flight webhook requests to finish queuing to Redis before the host
// stops. 30 s matches Railway's SIGTERM-to-SIGKILL grace period.
builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TradeFlow Guardian API", Version = "v1" }));

// ── CORS (React PWA dashboard) ────────────────────────────────────────────────
// Dashboard:Origin accepts a comma-separated list of allowed origins so that
// both the local dev server and the production Railway URL can be permitted
// without a wildcard (required when AllowCredentials is set).
builder.Services.AddCors(options =>
    options.AddPolicy("Dashboard", policy =>
        policy.WithOrigins(
                  (builder.Configuration["Dashboard:Origin"] ?? "http://localhost:5173")
                  .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));

var app = builder.Build();

// ── Startup config banner ─────────────────────────────────────────────────────
{
    var startupLog = app.Services.GetRequiredService<ILogger<Program>>();
    var oandaCfg   = app.Services.GetRequiredService<IOptions<OandaConfig>>().Value;
    var redisCfg   = app.Services.GetRequiredService<IOptions<RedisConfig>>().Value;
    var filterCfg  = app.Services.GetRequiredService<IOptions<FilterConfig>>().Value;
    startupLog.LogInformation(
        "API starting | OANDA={OandaEnv} | Url={BaseUrl} | Redis={Redis} | Stream={Stream} | AtrFilter={AtrFilter} | NewsFilter={NewsFilter} | Cors={CorsOrigin}",
        oandaCfg.Environment, oandaCfg.BaseUrl,
        RedisHost(redisCfg.ConnectionString), redisCfg.StreamName,
        filterCfg.EnableAtrSpikeFilter, filterCfg.EnableNewsFilter,
        builder.Configuration["Dashboard:Origin"] ?? "http://localhost:5173");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Dashboard");
app.UseHttpMetrics();   // records http_request_duration_seconds for all routes
app.UseMiddleware<HmacValidationMiddleware>();
app.UseAuthorization();
app.MapControllers();
// TODO: restrict to private network when Railway private networking is configured
app.MapMetrics();       // exposes /metrics for Prometheus scraping (no auth — internal network only)
app.MapGet("/", () => Results.Ok(new
{
    service = "TradeFlow Guardian API",
    version = "1.0.0",
    utc = DateTimeOffset.UtcNow
}));

app.Run();

// Strips auth credentials from both redis://user:pass@host and host:port formats.
static string RedisHost(string cs) =>
    System.Text.RegularExpressions.Regex.Replace(cs, @"redis://[^@]+@", "").Split(',')[0];
