// namespace TradeFlowGuardian.Domain.Entities.Strategies.Core;
//
// public sealed class MarketContext
// {
//     public IReadOnlyList<Candle> Candles { get; init; } = [];
//     public DateTime CurrentTime { get; init; }
//     public bool HasOpenPosition { get; init; }
//     public bool IsLongPosition { get; init; }
//     public decimal Spread { get; set; }
//
//     public int Positions { get; set; }
//
//     // New: identify instrument and strategy for filters/stats
//     public string Instrument { get; init; } = "";
//
//     public string StrategyName { get; init; } = "";
//
//     // Optional: the timeframe identifier of this context, e.g. "D1"
//     public string Timeframe { get; init; } = "";
//
//     // Injection points to retrieve additional data
//     // These can be wired by your backtest/live engines.
//     public Func<string, DateTime, DateTime, string?, IReadOnlyList<ExecutedTrade>>? ExecutedTradesProvider
//     {
//         get;
//         init;
//     }
//
//     public Func<string, int, MarketContext?>? HigherTimeframeProvider { get; init; }
//
//     // Cached indicator values to avoid recalculation
//     private readonly Dictionary<string, object> _cache = new();
//
//     public T GetOrCalculate<T>(string key, Func<T> calculator)
//     {
//         if (_cache.TryGetValue(key, out var cached))
//             return (T)cached;
//
//         var result = calculator();
//         _cache[key] = result!;
//         return result;
//     }
//
//     public void ClearCache() => _cache.Clear();
//
//     // Convenience helpers used by filters
//
//     // Returns simple moving average of Close (or selectable source) at the last bar.
//     public decimal? SMA(int period, Func<Candle, decimal>? selector = null)
//     {
//         selector ??= c => c.Close;
//         if (Candles.Count < period) return null;
//
//         var key = $"SMA:{period}:{selector.Method.GetHashCode()}:{Candles.Count}";
//         return GetOrCalculate(key, () =>
//         {
//             decimal sum = 0m;
//             for (int i = Candles.Count - period; i < Candles.Count; i++)
//                 sum += selector(Candles[i]);
//             return sum / period;
//         });
//     }
//
//     // Higher timeframe context snapshot (delegates to provider)
//     public MarketContext? GetHigherTimeframeContext(string targetTimeframe, int lookbackBars)
//         => HigherTimeframeProvider?.Invoke(targetTimeframe, lookbackBars);
//
//     // Executed trades query (delegates to provider)
//     public IReadOnlyList<ExecutedTrade> GetExecutedTrades(string instrument, DateTime fromUtc, DateTime toUtc,
//         string? strategyName)
//         => ExecutedTradesProvider?.Invoke(instrument, fromUtc, toUtc, strategyName) ?? Array.Empty<ExecutedTrade>();
// }
//
// // Minimal trade record used by frequency-limiting filters
// public sealed class ExecutedTrade
// {
//     public DateTime TimeUtc { get; init; }
//     public string Instrument { get; init; } = "";
//     public string StrategyName { get; init; } = "";
//     public bool IsEntry { get; init; }
//     public long Units { get; init; }
// }