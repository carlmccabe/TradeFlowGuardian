using TradeFlowGuardian.Core.Models;
using TradeFlowGuardian.Infrastructure.Calendar;

namespace TradeFlowGuardian.Tests;

public class ForexFactoryParserTests
{
    private const string SampleIcal = """
        BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//ForexFactory//EN
        BEGIN:VEVENT
        DTSTART:20260412T133000Z
        SUMMARY:High Impact Expected\nUSD Non-Farm Payrolls
        DESCRIPTION:USD
        END:VEVENT
        BEGIN:VEVENT
        DTSTART:20260412T140000Z
        SUMMARY:Medium Impact Expected\nEUR CPI Flash Estimate y/y
        DESCRIPTION:EUR
        END:VEVENT
        BEGIN:VEVENT
        DTSTART:20260412T150000Z
        SUMMARY:Low Impact Expected\nGBP BBA Mortgage Approvals
        DESCRIPTION:GBP
        END:VEVENT
        END:VCALENDAR
        """;

    [Fact]
    public void ParseIcal_CorrectlyMapsImpactLevels()
    {
        var events = ForexFactoryCalendarService.ParseIcal(SampleIcal);

        Assert.Equal(3, events.Count);
        Assert.Equal(ImpactLevel.High,   events[0].Impact);
        Assert.Equal(ImpactLevel.Medium, events[1].Impact);
        Assert.Equal(ImpactLevel.Low,    events[2].Impact);
    }

    [Fact]
    public void ParseIcal_CorrectlyExtractsCurrencies()
    {
        var events = ForexFactoryCalendarService.ParseIcal(SampleIcal);

        Assert.Equal("USD", events[0].Currency);
        Assert.Equal("EUR", events[1].Currency);
        Assert.Equal("GBP", events[2].Currency);
    }

    [Fact]
    public void ParseIcal_StripsImpactPrefixFromTitle()
    {
        var events = ForexFactoryCalendarService.ParseIcal(SampleIcal);

        Assert.DoesNotContain("Impact Expected", events[0].Title);
        Assert.Contains("Non-Farm Payrolls", events[0].Title);
    }

    [Fact]
    public void ParseImpact_UnknownPrefix_ReturnsUnknown()
    {
        var level = ForexFactoryCalendarService.ParseImpact("Some random summary");
        Assert.Equal(ImpactLevel.Unknown, level);
    }

    [Fact]
    public void ParseIcal_EmptyString_ReturnsEmptyList()
    {
        // Should not throw — fail open
        var events = ForexFactoryCalendarService.ParseIcal(string.Empty);
        Assert.Empty(events);
    }
}
