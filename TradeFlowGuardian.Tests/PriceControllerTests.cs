using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TradeFlowGuardian.Api.Controllers;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using Xunit;

namespace TradeFlowGuardian.Tests;

public class PriceControllerTests
{
    private readonly Mock<IOandaClient> _oandaMock;
    private readonly Mock<ILogger<PriceController>> _loggerMock;
    private readonly PriceController _controller;

    public PriceControllerTests()
    {
        _oandaMock = new Mock<IOandaClient>();
        _loggerMock = new Mock<ILogger<PriceController>>();
        _controller = new PriceController(_oandaMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetMidPrice_ReturnsOk_WhenPriceExists()
    {
        // Arrange
        var instrument = "EUR_USD";
        var expectedMid = 1.0850m;
        _oandaMock.Setup(x => x.GetMidPriceAsync(instrument, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMid);

        // Act
        var result = await _controller.GetMidPrice(instrument, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);
        
        var midProperty = value.GetType().GetProperty("mid");
        Assert.Equal(expectedMid, midProperty?.GetValue(value));
    }

    [Fact]
    public async Task GetMidPrice_Returns502_WhenPriceUnavailable()
    {
        // Arrange
        var instrument = "INVALID";
        _oandaMock.Setup(x => x.GetMidPriceAsync(instrument, It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        // Act
        var result = await _controller.GetMidPrice(instrument, CancellationToken.None);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetPriceSnapshot_ReturnsOk_WhenSnapshotExists()
    {
        // Arrange
        var instrument = "EUR_USD";
        var expectedSnapshot = new PriceSnapshot(instrument, 1.0845m, 1.0855m, 1.0850m, 0.0001m, DateTimeOffset.UtcNow);
        _oandaMock.Setup(x => x.GetPriceSnapshotAsync(instrument, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSnapshot);

        // Act
        var result = await _controller.GetPriceSnapshot(instrument, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedSnapshot, okResult.Value);
    }

    [Fact]
    public async Task GetPriceSnapshot_Returns502_WhenSnapshotUnavailable()
    {
        // Arrange
        var instrument = "INVALID";
        _oandaMock.Setup(x => x.GetPriceSnapshotAsync(instrument, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceSnapshot?)null);

        // Act
        var result = await _controller.GetPriceSnapshot(instrument, CancellationToken.None);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetBidPrice_ReturnsOk_WhenPriceExists()
    {
        // Arrange
        var instrument = "EUR_USD";
        var snapshot = new PriceSnapshot(instrument, 1.0845m, 1.0855m, 1.0850m, 0.0001m, DateTimeOffset.UtcNow);
        _oandaMock.Setup(x => x.GetPriceSnapshotAsync(instrument, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await _controller.GetBidPrice(instrument, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);
        
        var bidProperty = value.GetType().GetProperty("bid");
        Assert.Equal(snapshot.Bid, bidProperty?.GetValue(value));
    }

    [Fact]
    public async Task GetAskPrice_ReturnsOk_WhenPriceExists()
    {
        // Arrange
        var instrument = "EUR_USD";
        var snapshot = new PriceSnapshot(instrument, 1.0845m, 1.0855m, 1.0850m, 0.0001m, DateTimeOffset.UtcNow);
        _oandaMock.Setup(x => x.GetPriceSnapshotAsync(instrument, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await _controller.GetAskPrice(instrument, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);
        
        var askProperty = value.GetType().GetProperty("ask");
        Assert.Equal(snapshot.Ask, askProperty?.GetValue(value));
    }
}
