using Microsoft.OpenApi.Models;
using Prometheus;
using StackExchange.Redis;
using TradeFlowGuardian.Api.Middleware;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Infrastructure.Filters;
using TradeFlowGuardian.Infrastructure.Oanda;
using TradeFlowGuardian.Infrastructure.Queue;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<OandaConfig>(builder.Configuration.GetSection("Oanda"));
builder.Services.Configure<RiskConfig>(builder.Configuration.GetSection("Risk"));
builder.Services.Configure<FilterConfig>(builder.Configuration.GetSection("Filters"));
builder.Services.Configure<WebhookConfig>(builder.Configuration.GetSection("Webhook"));
builder.Services.Configure<RedisConfig>(builder.Configuration.GetSection("Redis"));

// ── HTTP Client ───────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<IOandaClient, OandaClient>();

// ── Redis ─────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379"));

// ── Core Services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ISignalQueue, RedisSignalQueue>();
builder.Services.AddScoped<IPositionSizer, PositionSizer>();

// ── Filters (evaluation order — cheapest first) ───────────────────────────────
builder.Services.AddScoped<SignalAgeFilter>();
builder.Services.AddScoped<AtrSpikeFilter>();
builder.Services.AddScoped<ISignalFilter, CompositeSignalFilter>(sp =>
    new CompositeSignalFilter(new List<ISignalFilter>
    {
        sp.GetRequiredService<SignalAgeFilter>(),
        sp.GetRequiredService<AtrSpikeFilter>()
    }));

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TradeFlow Guardian API", Version = "v1" }));

// ── CORS (React PWA dashboard) ────────────────────────────────────────────────
builder.Services.AddCors(options =>
    options.AddPolicy("Dashboard", policy =>
        policy.WithOrigins(builder.Configuration["Dashboard:Origin"] ?? "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));

var app = builder.Build();

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
