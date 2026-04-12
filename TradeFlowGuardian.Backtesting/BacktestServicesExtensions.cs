using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RestSharp;
using TradeFlowGuardian.Backtesting.Data;
using TradeFlowGuardian.Backtesting.Engine;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Infrastructure.Services;
using TradeFlowGuardian.Infrastructure.Services.Oanda;
using TradeFlowGuardian.Infrastructure.Services.Oanda.Configuration;

namespace TradeFlowGuardian.Backtesting;

public static class BacktestServicesExtensions
{
    /// <summary>
    /// Registers all backtest engine services:
    /// BacktestDataContext (EF Core → PostgreSQL), OandaApiService (historical candle feed),
    /// IHistoricalDataProvider, and IBacktestEngine.
    ///
    /// Requires these config keys to already exist:
    ///   Postgres:ConnectionString  — shared with the live trade history repository
    ///   Oanda:ApiKey / AccountId / Environment — shared with OandaClient
    /// </summary>
    public static IServiceCollection AddBacktestServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── OandaOptions: mapped from the same Oanda config section as OandaConfig ──
        services.Configure<OandaOptions>(opts =>
        {
            opts.ApiKey    = configuration["Oanda:ApiKey"] ?? string.Empty;
            opts.AccountId = configuration["Oanda:AccountId"] ?? string.Empty;
            var env        = configuration["Oanda:Environment"] ?? "fxpractice";
            opts.ApiUrl    = env == "fxtrade"
                ? "https://api-fxtrade.oanda.com"
                : "https://api-fxpractice.oanda.com";
        });

        // ── RestSharp client used by OandaHttpClient ──────────────────────────
        // Singleton: RestClient manages its own connection pool.
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<OandaOptions>>().Value;
            return new RestClient(opts.ApiUrl);
        });

        services.AddSingleton<OandaHttpClient>();
        services.AddScoped<IOandaApiService, OandaApiService>();

        // ── EF Core DbContext for backtest tables ─────────────────────────────
        // Uses the same Postgres connection string as the live trade history.
        // EF Core (backtest tables) and Dapper (live trade_history) coexist on
        // the same database — each operates on its own set of tables.
        var connectionString = configuration["Postgres:ConnectionString"] ?? string.Empty;
        services.AddDbContext<BacktestDataContext>(opts =>
            opts.UseNpgsql(connectionString));

        // ── Backtest engine services ──────────────────────────────────────────
        services.AddScoped<IHistoricalDataProvider, OandaHistoricalProvider>();
        services.AddScoped<IBacktestEngine, BacktestEngine>();

        return services;
    }
}
