using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using TradeFlowGuardian.Api.Controllers;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Tests;

public class AccountsControllerTests
{
    private const string Secret = "test-secret";

    private readonly Mock<IOandaAccountStore> _storeMock = new();
    private readonly Mock<IActiveAccountProvider> _providerMock = new();
    private readonly AccountsController _controller;

    public AccountsControllerTests()
    {
        var subMock = new Mock<ISubscriber>();
        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(subMock.Object);

        var services = new ServiceCollection();
        services.AddSingleton(_storeMock.Object);
        var provider = services.BuildServiceProvider();

        _controller = new AccountsController(
            provider,
            _providerMock.Object,
            redisMock.Object,
            Options.Create(new WebhookConfig { Secret = Secret }),
            Mock.Of<ILogger<AccountsController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private void Authenticate() =>
        _controller.Request.Headers["X-Admin-Secret"] = Secret;

    [Fact]
    public async Task GetAll_WithoutSecret_Returns401()
    {
        var result = await _controller.GetAll(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
        _storeMock.Verify(s => s.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAll_WithWrongSecret_Returns401()
    {
        _controller.Request.Headers["X-Admin-Secret"] = "wrong";

        var result = await _controller.GetAll(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithSecret_ReturnsAccounts()
    {
        Authenticate();
        _storeMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OandaAccount { Id = Guid.NewGuid(), Label = "Demo" }]);

        var result = await _controller.GetAll(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Create_ActivatingLiveWithoutConfirm_Returns400()
    {
        Authenticate();

        var result = await _controller.Create(
            new CreateAccountRequest("Live", "001-001", "fxtrade", "key", Activate: true, ConfirmLive: false),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        _storeMock.Verify(s => s.CreateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_InvalidEnvironment_Returns400()
    {
        Authenticate();

        var result = await _controller.Create(
            new CreateAccountRequest("X", "001-001", "production", "key"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Activate_LiveAccountWithoutConfirm_Returns400()
    {
        Authenticate();
        var id = Guid.NewGuid();
        _storeMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OandaAccount { Id = id, Label = "Live", Environment = "fxtrade" }]);

        var result = await _controller.Activate(id, new ActivateAccountRequest(ConfirmLive: false), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        _storeMock.Verify(s => s.ActivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Activate_PracticeAccount_SwitchesAndReturns200()
    {
        Authenticate();
        var id = Guid.NewGuid();
        var account = new OandaAccount { Id = id, Label = "Demo", Environment = "fxpractice", IsActive = true };
        _storeMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([account]);
        _storeMock.Setup(s => s.ActivateAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var result = await _controller.Activate(id, null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        _providerMock.Verify(p => p.Invalidate(), Times.Once);
    }

    [Fact]
    public async Task Delete_ActiveAccount_Returns409()
    {
        Authenticate();
        var id = Guid.NewGuid();
        _storeMock.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot delete the active account"));

        var result = await _controller.Delete(id, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }
}
