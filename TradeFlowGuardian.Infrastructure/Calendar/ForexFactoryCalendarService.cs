using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using IcalCalendar = Ical.Net.Calendar;

namespace TradeFlowGuardian.Infrastructure.Calendar;

/// <summary>
/// Fetches the ForexFactory economic calendar via their public iCal feed and caches it
/// in memory. The cache refreshes every <see cref="NewsFilterOptions.CacheRefreshHours"/> hours.
///
/// iCal field mapping:
///   SUMMARY  → "High Impact Expected\nCUR Event Title" (or Medium/Low/no prefix)
///   DESCRIPTION → "USD" (currency code, first non-empty line)
///   DTSTART  → scheduled release time (UTC)
///
/// Fail-open: any fetch or parse error returns an empty list and logs a warning.
/// </summary>
public sealed class ForexFactoryCalendarService(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<NewsFilterOptions> options,
    ILogger<ForexFactoryCalendarService> logger)
    : IEconomicCalendarService
{
    private const string CalendarUrl = "https://www.forexfactory.com/calendar/export?format=ical";
    public const string HttpClientName = "ForexFactory";

    private IReadOnlyList<EconomicEvent> _cache = [];
    private DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<EconomicEvent>> GetUpcomingEventsAsync(
        IEnumerable<string> currencies,
        TimeSpan lookahead,
        CancellationToken ct = default)
    {
        var allEvents = await GetOrRefreshCacheAsync(ct);

        if (allEvents.Count == 0)
            return allEvents;

        var now = DateTimeOffset.UtcNow;
        var windowStart = now - lookahead;
        var windowEnd = now + lookahead;
        var currencySet = currencies.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return allEvents
            .Where(e =>
                currencySet.Contains(e.Currency) &&
                e.ScheduledAt >= windowStart &&
                e.ScheduledAt <= windowEnd)
            .ToList();
    }

    // ── Cache management ──────────────────────────────────────────────────────

    private async Task<IReadOnlyList<EconomicEvent>> GetOrRefreshCacheAsync(CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow < _cacheExpiry)
            return _cache;

        await _lock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (DateTimeOffset.UtcNow < _cacheExpiry)
                return _cache;

            logger.LogInformation("Refreshing ForexFactory calendar cache");
            var events = await FetchAndParseAsync(ct);
            _cache = events;
            _cacheExpiry = DateTimeOffset.UtcNow.AddHours(options.CurrentValue.CacheRefreshHours);
            logger.LogInformation("Calendar cache refreshed — {Count} events loaded", events.Count);
            return _cache;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh ForexFactory calendar — using stale/empty cache");
            return _cache; // stale is better than nothing; empty if first load fails
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<EconomicEvent>> FetchAndParseAsync(CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient(HttpClientName);
        var icalText = await client.GetStringAsync(CalendarUrl, ct);
        return ParseIcal(icalText);
    }

    // ── iCal parsing ──────────────────────────────────────────────────────────

    internal static List<EconomicEvent> ParseIcal(string icalText)
    {
        var events = new List<EconomicEvent>();

        if (string.IsNullOrWhiteSpace(icalText))
            return events;

        // In Ical.Net 5.x, Calendar.Load(string) returns a single Calendar
        var calendar = IcalCalendar.Load(icalText);
        if (calendar is null)
            return events;

        foreach (var calEvent in calendar.Events)
        {
            var ev = ToEconomicEvent(calEvent);
            if (ev is not null)
                events.Add(ev);
        }

        return events;
    }

    private static EconomicEvent? ToEconomicEvent(CalendarEvent calEvent)
    {
        if (calEvent.DtStart is not CalDateTime)
            return null;

        var summary = calEvent.Summary ?? string.Empty;
        var description = calEvent.Description ?? string.Empty;

        var currency = ExtractCurrency(description);
        if (string.IsNullOrWhiteSpace(currency))
            return null;

        var impact = ParseImpact(summary);
        var title = ExtractTitle(summary);

        // Ical.Net 5.x: CalDateTime.AsUtc returns DateTime in UTC; wrap in DateTimeOffset
        var dtStart = calEvent.DtStart as CalDateTime;
        var scheduledAt = dtStart is not null
            ? new DateTimeOffset(dtStart.AsUtc, TimeSpan.Zero)
            : DateTimeOffset.MinValue;

        return new EconomicEvent(currency, title, scheduledAt, impact);
    }

    /// <summary>Currency code is the first non-empty line of the Description field.</summary>
    internal static string ExtractCurrency(string description)
    {
        foreach (var line in description.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                return trimmed;
        }
        return string.Empty;
    }

    /// <summary>
    /// ForexFactory encodes impact in the first line of SUMMARY.
    /// Lines after the prefix contain the event title.
    /// </summary>
    internal static ImpactLevel ParseImpact(string summary)
    {
        if (summary.Contains("High Impact Expected", StringComparison.OrdinalIgnoreCase))
            return ImpactLevel.High;
        if (summary.Contains("Medium Impact Expected", StringComparison.OrdinalIgnoreCase))
            return ImpactLevel.Medium;
        if (summary.Contains("Low Impact Expected", StringComparison.OrdinalIgnoreCase))
            return ImpactLevel.Low;
        return ImpactLevel.Unknown;
    }

    /// <summary>
    /// Strips the impact prefix line from the Summary, returning only the event title.
    /// E.g. "High Impact Expected\nUSD Non-Farm Payrolls" → "USD Non-Farm Payrolls"
    /// </summary>
    internal static string ExtractTitle(string summary)
    {
        var lines = summary.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.Contains("Impact Expected", StringComparison.OrdinalIgnoreCase))
                return trimmed;
        }
        return summary.Trim();
    }
}
