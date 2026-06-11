using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;

namespace TradeFlowGuardian.Infrastructure.Accounts;

/// <summary>
/// One-time migration path off env vars: if the registry is empty and the legacy
/// Oanda config section holds real credentials, seed it as the active account.
/// Registered in the Api only. Idempotent — does nothing once any account exists.
/// </summary>
public class AccountSeedService(
    IServiceScopeFactory scopeFactory,
    IOptions<OandaConfig> oandaConfig,
    ILogger<AccountSeedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetService<IOandaAccountStore>();
            if (store is null)
            {
                logger.LogWarning("Account registry unavailable (no Postgres) — seeding skipped");
                return;
            }

            var existing = await store.GetAllAsync(ct);
            if (existing.Count > 0) return;

            var cfg = oandaConfig.Value;
            if (!ActiveAccountProvider.IsUsable(cfg))
            {
                logger.LogWarning("Account registry empty and Oanda config holds no real credentials — register an account via POST /api/accounts");
                return;
            }

            await store.CreateAsync(
                label: $"Seeded from config ({cfg.Environment})",
                accountId: cfg.AccountId,
                environment: cfg.Environment,
                apiKey: cfg.ApiKey,
                activate: true,
                ct: ct);

            logger.LogInformation(
                "Seeded account registry from Oanda config ({Env} {AccountId}) — env vars can now be removed",
                cfg.Environment, cfg.AccountId);
        }
        catch (Exception ex)
        {
            // Never block startup on seeding — the provider still has the config fallback.
            logger.LogError(ex, "Account registry seeding failed");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
