using Microsoft.OpenApi.Models;
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

// ── HTTP Client ───────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<IOandaClient, OandaClient>();

// ── Core Services ─────────────────────────────────────────────────────────────
// Singleton queue — shared between API (writer) and Worker (reader)
builder.Services.AddSingleton<ISignalQueue, InMemorySignalQueue>();
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
app.UseMiddleware<HmacValidationMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    service = "TradeFlow Guardian API",
    version = "1.0.0",
    utc = DateTimeOffset.UtcNow
}));

app.Run();
