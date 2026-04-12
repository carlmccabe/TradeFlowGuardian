using TradeFlowGuardian.Domain.Entities.Strategies.Core;

namespace TradeFlowGuardian.Strategies.Filters;

public sealed class NoTradeTimeFilter : IFilter
{
    public string Name => "NoTradeTimeFilter";

    private readonly IReadOnlyList<(TimeSpan Start, TimeSpan End)> _allowedWindows;
    private readonly TimeZoneInfo _timeZone;

    public NoTradeTimeFilter(IEnumerable<(TimeSpan Start, TimeSpan End)> allowedWindows, TimeZoneInfo? timeZone = null)
    {
        _allowedWindows = allowedWindows.ToList();
        _timeZone = timeZone ?? TimeZoneInfo.Utc;
    }

    // public bool ShouldAllow(MarketContext context, SignalResult signal)
    // {
// // Normalize to UTC to satisfy ConvertTimeFromUtc requirements
//         var utcTime = context.CurrentTime.Kind switch
//         {
//             DateTimeKind.Utc => context.CurrentTime,
//             DateTimeKind.Local => context.CurrentTime.ToUniversalTime(),
//             DateTimeKind.Unspecified => DateTime.SpecifyKind(context.CurrentTime, DateTimeKind.Utc),
//             _ => context.CurrentTime
//         };
//
//         var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, _timeZone).TimeOfDay;
//
//         // If no windows configured, block nothing
//         if (_allowedWindows.Count == 0) return true;
//
//         foreach (var (start, end) in _allowedWindows)
//         {
//             if (start <= end)
//             {
//                 if (localTime >= start && localTime <= end) return true;
//             }
//             else
//             {
//                 // Overnight window (e.g., 22:00 - 02:00)
//                 if (localTime >= start || localTime <= end) return true;
//             }
//         }
//         return false;
//     }
public string Id { get; }
public string Description { get; }
public FilterResult Evaluate(IMarketContext context)
{
    throw new NotImplementedException();
}
}
