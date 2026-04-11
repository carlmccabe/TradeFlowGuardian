using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TradeFlowGuardian.Api.Controllers;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using Xunit;

namespace TradeFlowGuardian.Tests;

public class StatusControllerTests
{
    private readonly Mock<IOandaClient> _oandaMock;
    private readonly Mock<IPauseState> _pauseStateMock;
    private readonly Mock<IDailyDrawdownGuard> _drawdownGuardMock;
    private readonly Mock<ILogger<StatusController>> _loggerMock;
    private readonly StatusController _controller;

    public StatusControllerTests()
    {
        _oandaMock = new Mock<IOandaClient>();
        _pauseStateMock = new Mock<IPauseState>();
        _drawdownGuardMock = new Mock<IDailyDrawdownGuard>();
        _loggerMock = new Mock<ILogger<StatusController>>();

        var riskOptions = Options.Create(new RiskConfig { MaxDailyDrawdownPercent = 3.0m });
        _controller = new StatusController(
            _oandaMock.Object,
            _pauseStateMock.Object,
            _drawdownGuardMock.Object,
            riskOptions,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetBalance_ReturnsOk_WithBalance()
    {
        // Arrange
        var expectedBalance = 1000.50m;
        _oandaMock.Setup(x => x.GetAccountBalanceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBalance);

        // Act
        var result = await _controller.GetBalance(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);
        
        var balanceProperty = value.GetType().GetProperty("balanceAud");
        Assert.Equal(expectedBalance, balanceProperty?.GetValue(value));
    }

    [Theory]
    [InlineData(100, "LONG")]
    [InlineData(-50, "SHORT")]
    [InlineData(0, "FLAT")]
    public async Task GetPosition_ReturnsOk_WithCorrectSide(decimal units, string expectedSide)
    {
        // Arrange
        var instrument = "EUR_USD";
        _oandaMock.Setup(x => x.GetOpenPositionUnitsAsync(instrument, It.IsAny<CancellationToken>()))
            .ReturnsAsync(units);

        // Act
        var result = await _controller.GetPosition(instrument, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);

        var sideProperty = value.GetType().GetProperty("side");
        Assert.Equal(expectedSide, sideProperty?.GetValue(value));
        
        var unitsProperty = value.GetType().GetProperty("units");
        Assert.Equal(units, unitsProperty?.GetValue(value));
    }

    [Fact]
    public async Task ClosePosition_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var instrument = "EUR_USD";
        var expectedResult = new TradeResult { Success = true, Message = "Position closed" };
        _oandaMock.Setup(x => x.ClosePositionAsync(instrument, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.ClosePosition(instrument, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);

        var messageProperty = value.GetType().GetProperty("message");
        Assert.Equal("Position closed", messageProperty?.GetValue(value));
    }

    [Fact]
    public async Task ClosePosition_ReturnsBadRequest_WhenFailed()
    {
        // Arrange
        var instrument = "EUR_USD";
        var expectedResult = TradeResult.Failed("Close failed");
        _oandaMock.Setup(x => x.ClosePositionAsync(instrument, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.ClosePosition(instrument, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var value = badRequestResult.Value;
        Assert.NotNull(value);

        var errorProperty = value.GetType().GetProperty("error");
        Assert.Equal("Close failed", errorProperty?.GetValue(value));
    }
}
