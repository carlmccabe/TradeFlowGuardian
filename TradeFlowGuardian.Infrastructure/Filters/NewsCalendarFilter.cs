using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Filters;

/// <summary>
/// Blocks trade entries during high-impact economic news events sourced from
/// the ForexFactory iCal feed.
///
/// Logic:
///   1. Extract both currencies from the instrument (e.g. EUR_USD → EUR, USD)
///   2. Fetch upcoming events from <see cref="IEconomicCalendarService"/>
///   3. Block if any event with impact ≥ MinimumImpactLevel falls within
///      [now − BlockWindowMinutesAfter, now + BlockWindowMinutesBefore]
///
/// Fail-open: any exception is swallowed and logged — the filter never blocks
/// trading due to a calendar service failure.
/// </summary>
public sealed class NewsCalendarFilter : ISignalFilter
{
    private readonly IEconomicCalendarService _calendar;
    private readonly IOptionsMonitor<NewsFilterOptions> _options;
    private readonly ILogger<NewsCalendarFilter> _logger;

    public NewsCalendarFilter(
        IEconomicCalendarService calendar,
        IOptionsMonitor<NewsFilterOptions> options,
        ILogger<NewsCalendarFilter> logger)
    {
        _calendar = calendar;
        _options = options;
        _logger = logger;
    }

    public async Task<FilterResult> EvaluateAsync(TradeSignal signal, CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;

        if (!opts.Enabled)
            return FilterResult.Allow();

        try
        {
            var currencies = signal.Instrument
                .Split('_', StringSplitOptions.RemoveEmptyEntries);

            var lookahead = TimeSpan.FromMinutes(
                opts.BlockWindowMinutesBefore + opts.BlockWindowMinutesAfter);

            var events = await _calendar.GetUpcomingEventsAsync(currencies, lookahead, ct);

            var now = DateTimeOffset.UtcNow;
            var windowStart = now - TimeSpan.FromMinutes(opts.BlockWindowMinutesAfter);
            var windowEnd   = now + TimeSpan.FromMinutes(opts.BlockWindowMinutesBefore);

            var blocking = events
                .Where(e =>
                    e.Impact >= opts.MinimumImpactLevel &&
                    e.ScheduledAt >= windowStart &&
                    e.ScheduledAt <= windowEnd)
                .OrderBy(e => Math.Abs((e.ScheduledAt - now).TotalMinutes))
                .FirstOrDefault();

            if (blocking is not null)
            {
                var minutesAway = (blocking.ScheduledAt - now).TotalMinutes;
                var timeDesc = minutesAway > 0
                    ? $"in {(int)Math.Round(minutesAway)} min"
                    : $"{(int)Math.Round(-minutesAway)} min ago";

                var reason = $"News blackout: {blocking.Currency} {blocking.Title} {timeDesc}";
                _logger.LogWarning("Signal for {Instrument} blocked — {Reason}", signal.Instrument, reason);
                return FilterResult.Block(reason, "news_blackout");
            }

            return FilterResult.Allow();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "NewsCalendarFilter threw for {Instrument} — failing open",
                signal.Instrument);
            return FilterResult.Allow();
        }
    }
}
