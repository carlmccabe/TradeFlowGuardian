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
    public async Task HandleAsync_ShouldProcessNewSignal_AndIgnoreDuplicate()
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
            IdempotencyKey = "test-key-123",
            Price = 1.1000m,
            Atr = 0.0010m,
            Timestamp = DateTimeOffset.UtcNow
        };

        var key = $"idempotency:signal:{signal.IdempotencyKey}";

        // First call: Redis returns true (not exists)
        _dbMock.Setup(x => x.StringSetAsync(key, "1", TimeSpan.FromHours(24), When.NotExists, CommandFlags.None))
            .ReturnsAsync(true);

        // Setup OANDA and filters to avoid early exit if desired, but we mostly care about idempotency here
        _filterMock.Setup(x => x.EvaluateAsync(It.IsAny<TradeSignal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FilterResult { Allowed = false, Reason = "Stop for test" });

        // Act
        await handler.HandleAsync(signal, CancellationToken.None);

        // Assert
        _dbMock.Verify(x => x.StringSetAsync(key, "1", TimeSpan.FromHours(24), When.NotExists, CommandFlags.None), Times.Once);
        _filterMock.Verify(x => x.EvaluateAsync(signal, It.IsAny<CancellationToken>()), Times.Once);

        // Second call: Redis returns false (already exists)
        _dbMock.Setup(x => x.StringSetAsync(key, "1", TimeSpan.FromHours(24), When.NotExists, CommandFlags.None))
            .ReturnsAsync(false);

        // Act
        await handler.HandleAsync(signal, CancellationToken.None);

        // Assert
        _dbMock.Verify(x => x.StringSetAsync(key, "1", TimeSpan.FromHours(24), When.NotExists, CommandFlags.None), Times.Exactly(2));
        _filterMock.Verify(x => x.EvaluateAsync(signal, It.IsAny<CancellationToken>()), Times.Once); // Should NOT have been called a second time
    }
}
