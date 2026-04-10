using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Enums;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using TradeFlowGuardian.Worker.Handlers;
using Xunit;

namespace TradeFlowGuardian.Tests;

public class SignalExecutionHandlerTests
{
    private readonly Mock<ISignalFilter> _filterMock = new();
    private readonly Mock<IOandaClient> _oandaMock = new();
    private readonly Mock<IPositionSizer> _sizerMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<ILogger<SignalExecutionHandler>> _loggerMock = new();
    private readonly IOptions<RiskConfig> _riskOptions;

    public SignalExecutionHandlerTests()
    {
        _riskOptions = Options.Create(new RiskConfig());
        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldIgnoreDuplicateWhenRedisReturnsFalse()
    {
        // Arrange
        var handler = new SignalExecutionHandler(
            _filterMock.Object,
            _oandaMock.Object,
            _sizerMock.Object,
            _riskOptions,
            _redisMock.Object,
            _loggerMock.Object);

        var signal = new TradeSignal
        {
            Instrument = "EUR_USD",
            Direction = SignalDirection.Long,
            IdempotencyKey = "test-key-duplicate",
            Price = 1.1000m,
            Atr = 0.0010m,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Mock Redis to return false (key already exists)
        _dbMock.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), false, When.NotExists, CommandFlags.None))
            .ReturnsAsync(false);

        // Act
        await handler.HandleAsync(signal, CancellationToken.None);

        // Assert
        // If idempotency works, it should return early and NOT call the filter
        _filterMock.Verify(x => x.EvaluateAsync(It.IsAny<TradeSignal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldContinueWhenRedisReturnsTrue()
    {
        // Arrange
        var handler = new SignalExecutionHandler(
            _filterMock.Object,
            _oandaMock.Object,
            _sizerMock.Object,
            _riskOptions,
            _redisMock.Object,
            _loggerMock.Object);

        var signal = new TradeSignal
        {
            Instrument = "EUR_USD",
            Direction = SignalDirection.Long,
            IdempotencyKey = "test-key-new",
            Price = 1.1000m,
            Atr = 0.0010m,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Mock Redis to return true (key created)
        // We set up BOTH variants just to be absolutely sure
        _dbMock.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), false, When.NotExists, CommandFlags.None))
            .ReturnsAsync(true);
        _dbMock.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), When.NotExists, CommandFlags.None))
            .ReturnsAsync(true);

        _filterMock.Setup(x => x.EvaluateAsync(It.IsAny<TradeSignal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FilterResult { Allowed = false, Reason = "Stop for test" });

        // Act
        await handler.HandleAsync(signal, CancellationToken.None);

        // Assert
        // Should continue to filters
        _filterMock.Verify(x => x.EvaluateAsync(It.IsAny<TradeSignal>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
