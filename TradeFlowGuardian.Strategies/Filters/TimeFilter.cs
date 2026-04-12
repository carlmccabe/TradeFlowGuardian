using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Filters.Base;

namespace TradeFlowGuardian.Strategies.Filters;

/// <summary>
/// Filters based on time of day (market session)
/// </summary>
public sealed class TimeFilter : FilterBase
{
    private readonly TimeSpan _startTime;
    private readonly TimeSpan _endTime;
    private readonly TimeZoneInfo _timeZone;

    public TimeFilter(
        string id, 
        TimeSpan startTime, 
        TimeSpan endTime,
        string timeZoneId = "UTC")
        : base(id, $"Time {startTime:hh\\:mm}-{endTime:hh\\:mm} {timeZoneId}")
    {
        _startTime = startTime;
        _endTime = endTime;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    protected override FilterResult EvaluateCore(IMarketContext context)
    {
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(context.TimestampUtc, _timeZone);
        var currentTime = localTime.TimeOfDay;

        bool passed;
        if (_startTime < _endTime)
        {
            // Normal case: 09:00 - 17:00
            passed = currentTime >= _startTime && currentTime <= _endTime;
        }
        else
        {
            // Wraps midnight: 22:00 - 02:00
            passed = currentTime >= _startTime || currentTime <= _endTime;
        }

        return new FilterResult
        {
            Passed = passed,
            Reason = $"Current time {currentTime:hh\\:mm} {(passed ? "within" : "outside")} trading hours",
            EvaluatedAt = context.TimestampUtc,
            Diagnostics = new Dictionary<string, object>
            {
                ["CurrentTimeUTC"] = context.TimestampUtc.ToString("o"),
                ["LocalTime"] = localTime.ToString("o"),
                ["TimeOfDay"] = currentTime.ToString(),
                ["StartTime"] = _startTime.ToString(),
                ["EndTime"] = _endTime.ToString(),
                ["TimeZone"] = _timeZone.Id
            }
        };
    }
}