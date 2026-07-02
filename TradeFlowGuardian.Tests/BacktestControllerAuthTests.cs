using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TradeFlowGuardian.Api.Controllers;
using TradeFlowGuardian.Backtesting.Data;
using TradeFlowGuardian.Backtesting.Engine;
using TradeFlowGuardian.Core.Configuration;
using Xunit;

namespace TradeFlowGuardian.Tests;

/// <summary>
/// POST /api/backtest/run is CPU-heavy and can trigger OANDA fetches, so it requires
/// the admin secret. These tests pin that the gate rejects before touching the engine.
/// </summary>
public class BacktestControllerAuthTests
{
    private const string Secret = "test-secret";

    private readonly Mock<IBacktestEngine> _engineMock = new();
    private readonly BacktestController _controller;

    public BacktestControllerAuthTests()
    {
        // dbContext/dataProvider are only touched by the read endpoints, which stay open
        _controller = new BacktestController(
            _engineMock.Object,
            Mock.Of<IHistoricalDataProvider>(),
            null!,
            Options.Create(new WebhookConfig { Secret = Secret }),
            Mock.Of<ILogger<BacktestController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static BacktestApiRequest ValidRequest() => new()
    {
        Name = "auth-test",
        Instrument = "USD_JPY",
        Timeframe = "M15",
        StrategyPreset = "tfg_usdjpy_v5",
        StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task Run_WithoutSecret_Returns401_AndNeverRunsEngine()
    {
        var result = await _controller.RunBacktest(ValidRequest(), CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
        _engineMock.Verify(
            e => e.RunBacktestAsync(It.IsAny<BacktestRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_WithWrongSecret_Returns401()
    {
        _controller.Request.Headers["X-Admin-Secret"] = "wrong";

        var result = await _controller.RunBacktest(ValidRequest(), CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
    }

    [Fact]
    public async Task Run_WithCorrectSecret_ReachesTheEngine()
    {
        _controller.Request.Headers["X-Admin-Secret"] = Secret;
        _engineMock
            .Setup(e => e.RunBacktestAsync(It.IsAny<BacktestRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Backtesting.Models.BacktestResult { Name = "auth-test" });

        var result = await _controller.RunBacktest(ValidRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        _engineMock.Verify(
            e => e.RunBacktestAsync(It.IsAny<BacktestRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
