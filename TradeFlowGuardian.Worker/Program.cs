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

// ── Infrastructure ────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<IOandaClient, OandaClient>();
builder.Services.AddSingleton<ISignalQueue, InMemorySignalQueue>();
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

var host = builder.Build();
host.Run();
