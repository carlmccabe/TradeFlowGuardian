using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Infrastructure.Accounts;
using TradeFlowGuardian.Infrastructure.Data;

namespace TradeFlowGuardian.Tests;

/// <summary>Reversible fake — lets tests assert decryption without Data Protection.</summary>
internal class FakeSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => $"enc:{plaintext}";
    public string Unprotect(string ciphertext) => ciphertext["enc:".Length..];
}

public class OandaAccountRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TradeFlowDbContext _db;
    private readonly OandaAccountRepository _repo;

    public OandaAccountRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<TradeFlowDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new TradeFlowDbContext(options);
        _db.Database.EnsureCreated();

        _repo = new OandaAccountRepository(
            _db, new FakeSecretProtector(), Mock.Of<ILogger<OandaAccountRepository>>());
    }

    [Fact]
    public async Task Create_EncryptsApiKey_AtRest()
    {
        var account = await _repo.CreateAsync("Demo", "101-001", "fxpractice", "secret-key", activate: false);

        Assert.Equal("enc:secret-key", account.ApiKeyEncrypted);
        Assert.False(account.IsActive);
    }

    [Fact]
    public async Task CreateWithActivate_MakesAccountActive_AndGetActiveDecrypts()
    {
        await _repo.CreateAsync("Demo", "101-001", "fxpractice", "secret-key", activate: true);

        var active = await _repo.GetActiveAsync();

        Assert.NotNull(active);
        Assert.Equal("101-001", active.AccountId);
        Assert.Equal("secret-key", active.ApiKey);
        Assert.False(active.IsLive);
        Assert.EndsWith("fxpractice.oanda.com", active.BaseUrl);
    }

    [Fact]
    public async Task Activate_SwitchesActive_OnlyOneActiveRemains()
    {
        await _repo.CreateAsync("Demo", "101-001", "fxpractice", "demo-key", activate: true);
        var live = await _repo.CreateAsync("Live", "001-001", "fxtrade", "live-key", activate: false);

        var activated = await _repo.ActivateAsync(live.Id);

        Assert.NotNull(activated);
        Assert.True(activated.IsActive);

        var all = await _repo.GetAllAsync();
        Assert.Single(all, a => a.IsActive);

        var active = await _repo.GetActiveAsync();
        Assert.NotNull(active);
        Assert.Equal("001-001", active.AccountId);
        Assert.Equal("live-key", active.ApiKey);
        Assert.True(active.IsLive);
        Assert.EndsWith("fxtrade.oanda.com", active.BaseUrl);
    }

    [Fact]
    public async Task Activate_UnknownId_ReturnsNull()
    {
        Assert.Null(await _repo.ActivateAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Delete_ActiveAccount_Throws()
    {
        var account = await _repo.CreateAsync("Demo", "101-001", "fxpractice", "key", activate: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.DeleteAsync(account.Id));
    }

    [Fact]
    public async Task Delete_InactiveAccount_Succeeds()
    {
        var account = await _repo.CreateAsync("Demo", "101-001", "fxpractice", "key", activate: false);

        Assert.True(await _repo.DeleteAsync(account.Id));
        Assert.Empty(await _repo.GetAllAsync());
    }

    [Fact]
    public async Task GetActive_NoActiveAccount_ReturnsNull()
    {
        await _repo.CreateAsync("Demo", "101-001", "fxpractice", "key", activate: false);

        Assert.Null(await _repo.GetActiveAsync());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
