// using TradeFlowGuardian.Domain.Entities;
//
// namespace TradeFlowGuardian.Strategies;
//
// public static class Indicators
// {
//     public static decimal SMA(IReadOnlyList<decimal> series, int n)
//         => series.Count >= n ? series.Skip(series.Count - n).Average() : 0m;
//
//     public static decimal EMA(IReadOnlyList<decimal> series, int n)
//     {
//         if (series.Count == 0) return 0m;
//         var k = 2m / (n + 1);
//         decimal ema = series[0];
//         for (int i = 1; i < series.Count; i++) ema = series[i] * k + ema * (1 - k);
//         return ema;
//     }
//
//     public static decimal RSI(IReadOnlyList<decimal> closes, int n = 14)
//     {
//         if (closes.Count <= n) return 50m;
//         decimal gain = 0, loss = 0;
//         for (int i = closes.Count - n; i < closes.Count; i++)
//         {
//             var ch = closes[i] - closes[i - 1];
//             if (ch >= 0) gain += (decimal)ch; else loss += (decimal)(-ch);
//         }
//         if (loss == 0) return 100m;
//         var rs = (gain / n) / (loss / n);
//         return 100m - (100m / (1 + rs));
//     }
//
//     public static decimal ATR(IReadOnlyList<Candle> candles, int n = 14)
//     {
//         if (candles.Count < n + 1) return 0m;
//         var trs = new List<decimal>();
//         for (int i = candles.Count - n; i < candles.Count; i++)
//         {
//             var c = candles[i];
//             var prev = candles[i - 1];
//             var tr = new[] { (c.High - c.Low), Math.Abs(c.High - prev.Close), Math.Abs(c.Low - prev.Close) }.Max();
//             trs.Add(tr);
//         }
//         return trs.Average();
//     }
// }
//
// public sealed class EmacStrategy : IStrategy
// {
//     public string Name => "EMAC";
//
//     // Tunables (match your ranges) :contentReference[oaicite:3]{index=3}
//     private readonly int fast = 10, slow = 20, emaTrend = 200, rsiLen = 14, atrLen = 14;
//     private readonly decimal rsiLongMin = 50m, rsiShortMax = 50m;
//     private readonly decimal rr = 1.5m;
//
//     public Decision Evaluate(IReadOnlyList<Candle> m5, DateTime nowUtc, bool hasOpen, bool isLong)
//     {
//         if (m5.Count < Math.Max(emaTrend, Math.Max(slow, Math.Max(rsiLen, atrLen))) + 2)
//             return new Decision(TradeAction.Hold, Reason: "Warmup");
//
//         var closes = m5.Select(c => c.Close).ToList();
//         var ema200 = Indicators.EMA(closes, emaTrend);
//         var smaFastPrev = Indicators.SMA(closes.Take(closes.Count - 1).ToList(), fast);
//         var smaSlowPrev = Indicators.SMA(closes.Take(closes.Count - 1).ToList(), slow);
//         var smaFast = Indicators.SMA(closes, fast);
//         var smaSlow = Indicators.SMA(closes, slow);
//         var rsi = Indicators.RSI(closes, rsiLen);
//         var atr = Indicators.ATR(m5, atrLen);
//
//         bool upTrend = closes.Last() > ema200;
//         bool dnTrend = closes.Last() < ema200;
//
//         // Calculate EMA of actual historical ATR values
//         var atrValues = new List<decimal>();
//         for (int i = Math.Max(atrLen, m5.Count - 50); i < m5.Count; i++)
//         {
//             var historicalCandles = m5.Take(i + 1).ToList();
//             if (historicalCandles.Count >= atrLen + 1)
//             {
//                 atrValues.Add(Indicators.ATR(historicalCandles, atrLen));
//             }
//         }
//
//         var atrFloor = atrValues.Count >= 20
//             ? Indicators.EMA(atrValues, 20)
//             : Indicators.EMA(atrValues, atrValues.Count);
//         
//         bool volOk = atr > (atrFloor * 0.95m);
//
//         // Crossovers
//         bool bullCross = smaFastPrev <= smaSlowPrev && smaFast > smaSlow;
//         bool bearCross = smaFastPrev >= smaSlowPrev && smaFast < smaSlow;
//
//         if (!hasOpen && upTrend && bullCross && rsi > rsiLongMin && volOk)
//         {
//             var sl = Math.Max(m5.Last().Close - atr, m5.Last().Low);
//             var stopDist = m5.Last().Close - sl;
//             var tp = m5.Last().Close + (stopDist * rr);
//             return new Decision(TradeAction.Buy, sl, tp, "EMAC long");
//         }
//
//         if (!hasOpen && dnTrend && bearCross && rsi < rsiShortMax && volOk)
//         {
//             var sl = Math.Min(m5.Last().Close + atr, m5.Last().High);
//             var stopDist = sl - m5.Last().Close;
//             var tp = m5.Last().Close - (stopDist * rr);
//             return new Decision(TradeAction.Sell, sl, tp, "EMAC short");
//         }
//
//         // Early exit if opposite crossover + RSI flips centerline (per your spec) :contentReference[oaicite:4]{index=4}
//         if (hasOpen && isLong && bearCross && rsi < 50m) return new Decision(TradeAction.Exit, Reason: "Opposite signal");
//         if (hasOpen && !isLong && bullCross && rsi > 50m) return new Decision(TradeAction.Exit, Reason: "Opposite signal");
//
//         return new Decision(TradeAction.Hold);
//     }
// }
