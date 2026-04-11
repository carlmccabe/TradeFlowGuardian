using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Core.Interfaces;

public interface ISignalQueue
{
    Task EnqueueAsync(TradeSignal signal, CancellationToken ct = default);
    Task<TradeSignal?> DequeueAsync(CancellationToken ct = default);
}

public interface ISignalFilter
{
    Task<FilterResult> EvaluateAsync(TradeSignal signal, CancellationToken ct = default);
}

public interface IOandaClient
{
    Task<TradeResult> PlaceMarketOrderAsync(TradeSignal signal, decimal stopLoss, decimal takeProfit, long units, CancellationToken ct = default);
    Task<TradeResult> ClosePositionAsync(string instrument, CancellationToken ct = default);
    Task<decimal> GetAccountBalanceAsync(CancellationToken ct = default);
    Task<decimal?> GetOpenPositionUnitsAsync(string instrument, CancellationToken ct = default);
    Task<decimal?> GetMidPriceAsync(string instrument, CancellationToken ct = default);
    Task<PriceSnapshot?> GetPriceSnapshotAsync(string instrument, CancellationToken ct = default);
    /// <summary>Returns all instruments with an open position. Empty list on failure.</summary>
    Task<IReadOnlyList<OpenPositionSummary>> GetAllOpenPositionsAsync(CancellationToken ct = default);
}

public interface IPositionSizer
{
    Task<long> CalculateUnitsAsync(TradeSignal signal, decimal accountBalance, CancellationToken ct = default);
}

/// <summary>
/// Provides scheduled economic events for one or more currency codes.
/// Implementations are responsible for caching — callers should not throttle calls.
/// </summary>
public interface IEconomicCalendarService
{
    /// <summary>
    /// Returns upcoming events for the given currencies within ±<paramref name="lookahead"/> of now.
    /// Never throws — returns empty list on failure.
    /// </summary>
    Task<IReadOnlyList<EconomicEvent>> GetUpcomingEventsAsync(
        IEnumerable<string> currencies,
        TimeSpan lookahead,
        CancellationToken ct = default);
}

/// <summary>
/// Redis-backed global pause flag. When paused, all new Long/Short entries are blocked.
/// Persists across Worker restarts; only cleared by an explicit resume call.
/// </summary>
public interface IPauseState
{
    Task<bool> IsPausedAsync(CancellationToken ct = default);
    Task SetPausedAsync(bool paused, CancellationToken ct = default);
}

/// <summary>
/// Redis-backed daily drawdown circuit breaker.
/// Tracks day-open NAV and pauses new entries when drawdown exceeds the configured limit.
/// State is date-keyed in Redis and resets automatically at UTC midnight.
/// </summary>
public interface IDailyDrawdownGuard
{
    /// <summary>Returns true if today's drawdown limit has been breached.</summary>
    Task<bool> IsBreachedAsync(CancellationToken ct = default);

    /// <summary>
    /// Records today's day-open NAV using Redis SetNX. Safe to call on every signal —
    /// only the first call per UTC day has any effect.
    /// </summary>
    Task EnsureDayOpenNavAsync(decimal currentBalance, CancellationToken ct = default);

    /// <summary>
    /// Compares <paramref name="currentBalance"/> to day-open NAV. If drawdown exceeds
    /// the configured limit, sets the breached flag in Redis and logs a warning.
    /// </summary>
    /// <returns>True if the limit is breached (now or was already breached).</returns>
    Task<bool> CheckAndMarkIfBreachedAsync(decimal currentBalance, CancellationToken ct = default);

    /// <summary>Returns today's day-open NAV from Redis, or null if not yet recorded.</summary>
    Task<decimal?> GetDayOpenNavAsync(CancellationToken ct = default);
}

/// <summary>
/// Persists every order attempt (entry or close) to PostgreSQL for audit and P&amp;L history.
/// Implementations must never throw — a write failure must not abort the trade workflow.
/// </summary>
public interface ITradeHistoryRepository
{
    /// <summary>
    /// Inserts a trade record. Called after every PlaceMarketOrderAsync / ClosePositionAsync
    /// regardless of success or failure.
    /// </summary>
    Task InsertAsync(TradeHistoryRecord record, CancellationToken ct = default);
}

/// <summary>
/// Redis-backed cache for open position state per instrument.
/// Avoids an OANDA API round-trip on every signal when position state is already known.
/// Cache miss falls back to live OANDA query; callers must update the cache after trades.
/// </summary>
public interface IPositionCache
{
    /// <summary>Returns (true, units) if cached, (false, null) on cache miss.</summary>
    Task<(bool Found, decimal? Units)> GetAsync(string instrument, CancellationToken ct = default);

    /// <summary>Writes position units to cache. Call after a successful order placement.</summary>
    Task SetAsync(string instrument, decimal units, CancellationToken ct = default);

    /// <summary>Removes the cache entry. Call after a successful position close.</summary>
    Task ClearAsync(string instrument, CancellationToken ct = default);
}
