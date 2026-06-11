using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TradeFlowGuardian.Core.Interfaces;

namespace TradeFlowGuardian.Infrastructure.Accounts;

public static class AccountManagementServiceExtensions
{
    /// <summary>
    /// Registers the OANDA account registry: Data Protection (keys in Redis so Api
    /// and Worker share them), the encrypted store (requires TradeFlowDbContext),
    /// and the singleton active-account provider used by OandaClient.
    /// </summary>
    /// <param name="hasPostgres">When false the store is skipped and the provider falls back to the Oanda config section.</param>
    public static IServiceCollection AddAccountManagement(
        this IServiceCollection services,
        IConnectionMultiplexer redis,
        bool hasPostgres)
    {
        services.AddDataProtection()
            .SetApplicationName("TradeFlowGuardian")
            .PersistKeysToStackExchangeRedis(redis, "tradeflow:dataprotection-keys");

        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        if (hasPostgres)
            services.AddScoped<IOandaAccountStore, OandaAccountRepository>();

        services.AddSingleton<IActiveAccountProvider, ActiveAccountProvider>();
        return services;
    }
}
