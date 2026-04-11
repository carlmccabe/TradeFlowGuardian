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
    private readonly Mock<IPositionCache> _positionCacheMock = new();
    private readonly Mock<IDailyDrawdownGuard> _drawdownGuardMock = new();
    private readonly Mock<ITradeHistoryRepository> _tradeHistoryMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<ILogger<SignalExecutionHandler>> _loggerMock = new();
    private readonly IOptions<RiskConfig> _riskOptions;

    public SignalExecutionHandlerTests()
    {
        _riskOptions = Options.Create(new RiskConfig
        {
            DefaultRiskPercent = 1.0m,
            MaxPositionUnits = 1000000,
            AtrStopMultiplier = 2.0m,
            AtrTargetMultiplier = 4.0m,
            MaxDailyDrawdownPercent = 3.0m
        });

        _redisMock.Setup(x => x.GetDatabase())
            .Returns(_dbMock.Object);
        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);

        // Default: cache miss — falls through to OANDA
        _positionCacheMock
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (decimal?)null));

        // Default: drawdown not breached
        _drawdownGuardMock
            .Setup(x => x.IsBreachedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _drawdownGuardMock
            .Setup(x => x.EnsureDayOpenNavAsync(It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _drawdownGuardMock
            .Setup(x => x.CheckAndMarkIfBreachedAsync(It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Default: trade history writes succeed silently
        _tradeHistoryMock
            .Setup(x => x.InsertAsync(It.IsAny<TradeHistoryRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task HandleAsync_ShouldIgnoreDuplicateWhenRedisReturnsFalse()
    {
        // Arrange
        var handler = new SignalExecutionHandler(
            _filterMock.Object,
            _oandaMock.Object,
            _sizerMock.Object,
            _positionCacheMock.Object,
            _drawdownGuardMock.Object,
            _tradeHistoryMock.Object,
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
        _dbMock.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);
        _dbMock.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>()))
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
            _positionCacheMock.Object,
            _drawdownGuardMock.Object,
            _tradeHistoryMock.Object,
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

        // Mock Redis to return true (key created) using the exact signature called in production
        _dbMock.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(true);

        _filterMock.Setup(x => x.EvaluateAsync(It.IsAny<TradeSignal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FilterResult.Allow());

        _oandaMock.Setup(x => x.GetOpenPositionUnitsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        _oandaMock.Setup(x => x.GetAccountBalanceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(10000m);

        _sizerMock.Setup(x =>
                x.CalculateUnitsAsync(It.IsAny<TradeSignal>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000L);

        _oandaMock.Setup(x => x.PlaceMarketOrderAsync(It.IsAny<TradeSignal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TradeResult.Succeeded("123", 1.1001m, 1000, 1.0980m, 1.1040m));

        // Act
        await handler.HandleAsync(signal, CancellationToken.None);

        // Assert
        _oandaMock.Verify(
            x => x.PlaceMarketOrderAsync(It.IsAny<TradeSignal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldAbortWhenTPisInvalid()
    {
        // Arrange
        var riskOptions = Options.Create(new RiskConfig
        {
            AtrTargetMultiplier = -1.0m // Force invalid TP
        });

        var handler = new SignalExecutionHandler(
            _filterMock.Object,
            _oandaMock.Object,
            _sizerMock.Object,
            _positionCacheMock.Object,
            _drawdownGuardMock.Object,
            _tradeHistoryMock.Object,
            riskOptions,
            _redisMock.Object,
            _loggerMock.Object);

        var signal = new TradeSignal
        {
            Instrument = "EUR_USD",
            Direction = SignalDirection.Long,
            IdempotencyKey = "test-key-invalid-tp",
            Price = 1.1000m,
            Atr = 0.0010m,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Mock Redis to return true (key created)
        _dbMock.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _dbMock.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _filterMock.Setup(x => x.EvaluateAsync(It.IsAny<TradeSignal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FilterResult.Allow());

        _oandaMock.Setup(x => x.GetOpenPositionUnitsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        _oandaMock.Setup(x => x.GetAccountBalanceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(10000m);

        _sizerMock.Setup(x =>
                x.CalculateUnitsAsync(It.IsAny<TradeSignal>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000L);

        // Act
        await handler.HandleAsync(signal, CancellationToken.None);

        // Assert
        // Should NOT call PlaceMarketOrderAsync because it aborted at sanity check
        _oandaMock.Verify(
            x => x.PlaceMarketOrderAsync(It.IsAny<TradeSignal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldContinueWhenEverythingIsValid()
    {
        // Arrange
        // var ri
        var handler = new SignalExecutionHandler(
            _filterMock.Object,
            _oandaMock.Object,
            _sizerMock.Object,
            _positionCacheMock.Object,
            _drawdownGuardMock.Object,
            _tradeHistoryMock.Object,
            _riskOptions,
            _redisMock.Object,
            _loggerMock.Object);

        var signal = new TradeSignal
        {
            Instrument = "EUR_USD",
            Direction = SignalDirection.Long,
            IdempotencyKey = "test-key-valid",
            Price = 1.1000m,
            Atr = 0.0010m,
            Timestamp = DateTimeOffset.UtcNow
        };

        _dbMock.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
            It.IsAny<When>())).ReturnsAsync(true);

        _filterMock.Setup(x => x.EvaluateAsync(It.IsAny<TradeSignal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FilterResult.Allow());

        _oandaMock.Setup(x => x.GetOpenPositionUnitsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        _oandaMock.Setup(x => x.GetAccountBalanceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(10000m);

        _sizerMock.Setup(x =>
                x.CalculateUnitsAsync(It.IsAny<TradeSignal>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000L);

        _oandaMock.Setup(x => x.PlaceMarketOrderAsync(It.IsAny<TradeSignal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TradeResult.Succeeded("123", 1.1001m, 1000, 1.0980m, 1.1040m));

        // Act
        await handler.HandleAsync(signal, CancellationToken.None);

        // Assert
        _oandaMock.Verify(
            x => x.PlaceMarketOrderAsync(
                It.IsAny<TradeSignal>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}