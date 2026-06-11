using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using TradeFlowGuardian.Infrastructure.Accounts;

namespace TradeFlowGuardian.Tests;

public class ActiveAccountProviderTests
{
    private static IConnectionMultiplexer RedisMock()
    {
        var sub = new Mock<ISubscriber>();
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetSubscriber(It.IsAny<object>())).Returns(sub.Object);
        return mux.Object;
    }

    private static IServiceScopeFactory ScopeFactoryWith(IOandaAccountStore? store)
    {
        var services = new ServiceCollection();
        if (store is not null)
            services.AddScoped(_ => store);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static ActiveAccountProvider CreateProvider(IOandaAccountStore? store, OandaConfig config)
        => new(
            ScopeFactoryWith(store),
            RedisMock(),
            Options.Create(config),
            Mock.Of<ILogger<ActiveAccountProvider>>());

    private static readonly OandaConfig PlaceholderConfig = new()
    {
        ApiKey = "REPLACE_WITH_OANDA_API_KEY",
        AccountId = "REPLACE_WITH_ACCOUNT_ID"
    };

    [Fact]
    public async Task GetActive_ReturnsRegistryAccount_WhenStoreHasOne()
    {
        var store = new Mock<IOandaAccountStore>();
        store.Setup(s => s.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActiveOandaAccount("101-001", "key", "fxpractice", "Demo"));

        var provider = CreateProvider(store.Object, PlaceholderConfig);
        var account = await provider.GetActiveAsync();

        Assert.Equal("101-001", account.AccountId);
        Assert.Equal("Demo", account.Label);
    }

    [Fact]
    public async Task GetActive_FallsBackToConfig_WhenRegistryEmpty()
    {
        var store = new Mock<IOandaAccountStore>();
        store.Setup(s => s.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActiveOandaAccount?)null);

        var config = new OandaConfig { ApiKey = "real-key", AccountId = "101-002", Environment = "fxpractice" };
        var provider = CreateProvider(store.Object, config);

        var account = await provider.GetActiveAsync();

        Assert.Equal("101-002", account.AccountId);
        Assert.Equal("config-fallback", account.Label);
    }

    [Fact]
    public async Task GetActive_Throws_WhenNoRegistryAndPlaceholderConfig()
    {
        var store = new Mock<IOandaAccountStore>();
        store.Setup(s => s.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActiveOandaAccount?)null);

        var provider = CreateProvider(store.Object, PlaceholderConfig);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetActiveAsync());
    }

    [Fact]
    public async Task GetActive_CachesResult_UntilInvalidated()
    {
        var calls = 0;
        var store = new Mock<IOandaAccountStore>();
        store.Setup(s => s.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                calls++;
                return new ActiveOandaAccount($"acct-{calls}", "key", "fxpractice", "Demo");
            });

        var provider = CreateProvider(store.Object, PlaceholderConfig);

        var first = await provider.GetActiveAsync();
        var second = await provider.GetActiveAsync();
        Assert.Equal(first.AccountId, second.AccountId);
        Assert.Equal(1, calls);

        provider.Invalidate();
        var third = await provider.GetActiveAsync();
        Assert.Equal("acct-2", third.AccountId);
    }

    [Fact]
    public async Task GetActive_WorksWithoutStoreRegistered_UsingConfig()
    {
        var config = new OandaConfig { ApiKey = "real-key", AccountId = "101-003", Environment = "fxtrade" };
        var provider = CreateProvider(store: null, config);

        var account = await provider.GetActiveAsync();

        Assert.Equal("101-003", account.AccountId);
        Assert.True(account.IsLive);
    }
}
