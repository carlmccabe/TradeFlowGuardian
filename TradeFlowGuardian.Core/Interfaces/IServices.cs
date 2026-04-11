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
