using TradeFlowGuardian.Domain.Entities.Strategies.Core;

namespace TradeFlowGuardian.Strategies.Filters.Old;

public sealed class DayOfWeekFilter : IFilter
{
    private readonly HashSet<DayOfWeek> _allowedDays;

    /// <summary>
    /// Creates a new day-of-week filter
    /// </summary>
    /// <param name="id">Unique identifier for this filter</param>
    /// <param name="allowedDays">Days of the week on which trading is allowed</param>
    /// <exception cref="ArgumentNullException">Thrown when allowedDays is null</exception>
    /// <exception cref="ArgumentException">Thrown when allowedDays is empty</exception>
    public DayOfWeekFilter(string id, IEnumerable<DayOfWeek> allowedDays)
    {
        ArgumentNullException.ThrowIfNull(allowedDays);

        _allowedDays = new HashSet<DayOfWeek>(allowedDays);

        if (_allowedDays.Count == 0)
            throw new ArgumentException("At least one day must be allowed", nameof(allowedDays));

        Id = id ?? throw new ArgumentNullException(nameof(id));

        // Build a human-readable description
        var dayNames = _allowedDays.OrderBy(d => (int)d).Select(d => d.ToString());
        Description = $"DayOfWeek({string.Join(", ", dayNames)})";
    }

    public string Id { get; }
    public string Description { get; }

    public FilterResult Evaluate(IMarketContext context)
    {
        var currentDay = context.TimestampUtc.DayOfWeek;
        var isAllowed = _allowedDays.Contains(currentDay);

        return new FilterResult
        {
            Passed = isAllowed,
            Reason = isAllowed
                ? $"{currentDay} is an allowed trading day"
                : $"{currentDay} is not in allowed days ({string.Join(", ", _allowedDays.OrderBy(d => (int)d))})",
            EvaluatedAt = context.TimestampUtc,
            Diagnostics = new Dictionary<string, object>
            {
                ["CurrentDay"] = currentDay.ToString(),
                ["AllowedDays"] = _allowedDays.OrderBy(d => (int)d).Select(d => d.ToString()).ToArray(),
                ["TimestampUtc"] = context.TimestampUtc.ToString("o")
            }
        };
    }
}