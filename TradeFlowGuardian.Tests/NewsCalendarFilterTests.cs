using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Enums;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using TradeFlowGuardian.Infrastructure.Filters;

namespace TradeFlowGuardian.Tests;

public class NewsCalendarFilterTests
{
    private static readonly TradeSignal EurUsdSignal = new()
    {
        Instrument = "EUR_USD",
        Direction = SignalDirection.Long,
        Price = 1.1000m,
        Atr = 0.0010m,
        Timestamp = DateTimeOffset.UtcNow,
        IdempotencyKey = "test-news-filter"
    };

    private static IOptionsMonitor<NewsFilterOptions> DefaultOptions(
        Action<NewsFilterOptions>? configure = null)
    {
        var opts = new NewsFilterOptions
        {
            Enabled = true,
            BlockWindowMinutesBefore = 30,
            BlockWindowMinutesAfter = 30,
            MinimumImpactLevel = ImpactLevel.High,
            CacheRefreshHours = 6
        };
        configure?.Invoke(opts);

        var mock = new Mock<IOptionsMonitor<NewsFilterOptions>>();
        mock.SetupGet(m => m.CurrentValue).Returns(opts);
        return mock.Object;
    }

    private static NewsCalendarFilter BuildFilter(
        IEconomicCalendarService calendarService,
        IOptionsMonitor<NewsFilterOptions>? options = null)
        => new(
            calendarService,
            options ?? DefaultOptions(),
            NullLogger<NewsCalendarFilter>.Instance);

    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HighImpactEvent_InsideWindow_IsBlocked()
    {
        // Arrange — USD NFP fires in 10 minutes (inside 30-min before window)
        var now = DateTimeOffset.UtcNow;
        var events = new List<EconomicEvent>
        {
            new("USD", "Nonfarm Payrolls", now.AddMinutes(10), ImpactLevel.High)
        };

        var calendarMock = new Mock<IEconomicCalendarService>();
        calendarMock
            .Setup(c => c.GetUpcomingEventsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        var filter = BuildFilter(calendarMock.Object);

        // Act
        var result = await filter.EvaluateAsync(EurUsdSignal, CancellationToken.None);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal("news_blackout", result.Label);
        Assert.Contains("Nonfarm Payrolls", result.Reason);
    }

    [Fact]
    public async Task HighImpactEvent_OutsideWindow_IsAllowed()
    {
        // Arrange — event fires in 90 minutes (outside 30-min before window)
        var now = DateTimeOffset.UtcNow;
        var events = new List<EconomicEvent>
        {
            new("USD", "Nonfarm Payrolls", now.AddMinutes(90), ImpactLevel.High)
        };

        var calendarMock = new Mock<IEconomicCalendarService>();
        calendarMock
            .Setup(c => c.GetUpcomingEventsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        var filter = BuildFilter(calendarMock.Object);

        // Act
        var result = await filter.EvaluateAsync(EurUsdSignal, CancellationToken.None);

        // Assert
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task MediumImpactEvent_BelowThreshold_IsAllowed()
    {
        // Arrange — medium impact event inside window, but threshold is High
        var now = DateTimeOffset.UtcNow;
        var events = new List<EconomicEvent>
        {
            new("USD", "Core Retail Sales", now.AddMinutes(5), ImpactLevel.Medium)
        };

        var calendarMock = new Mock<IEconomicCalendarService>();
        calendarMock
            .Setup(c => c.GetUpcomingEventsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        var filter = BuildFilter(calendarMock.Object);

        // Act
        var result = await filter.EvaluateAsync(EurUsdSignal, CancellationToken.None);

        // Assert
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task CalendarServiceThrows_FailsOpen()
    {
        // Arrange — simulate calendar service being unreachable
        var calendarMock = new Mock<IEconomicCalendarService>();
        calendarMock
            .Setup(c => c.GetUpcomingEventsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("ForexFactory unreachable"));

        var filter = BuildFilter(calendarMock.Object);

        // Act — should not throw
        var result = await filter.EvaluateAsync(EurUsdSignal, CancellationToken.None);

        // Assert — fail open
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task FilterDisabled_SkipsCheck_ReturnsAllowed()
    {
        // Arrange — filter disabled; calendar should never be called
        var calendarMock = new Mock<IEconomicCalendarService>();

        var filter = BuildFilter(calendarMock.Object, DefaultOptions(o => o.Enabled = false));

        // Act
        var result = await filter.EvaluateAsync(EurUsdSignal, CancellationToken.None);

        // Assert
        Assert.True(result.Allowed);
        calendarMock.Verify(
            c => c.GetUpcomingEventsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HighImpactEvent_JustAfterWindow_IsBlocked()
    {
        // Arrange — event was 10 minutes ago (inside 30-min after window)
        var now = DateTimeOffset.UtcNow;
        var events = new List<EconomicEvent>
        {
            new("EUR", "ECB Rate Decision", now.AddMinutes(-10), ImpactLevel.High)
        };

        var calendarMock = new Mock<IEconomicCalendarService>();
        calendarMock
            .Setup(c => c.GetUpcomingEventsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        var filter = BuildFilter(calendarMock.Object);

        // Act
        var result = await filter.EvaluateAsync(EurUsdSignal, CancellationToken.None);

        // Assert
        Assert.False(result.Allowed);
        Assert.Contains("ago", result.Reason);
    }
}
