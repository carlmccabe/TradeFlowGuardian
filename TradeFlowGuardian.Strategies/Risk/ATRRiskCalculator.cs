// using TradeFlowGuardian.Domain.Entities;
// using TradeFlowGuardian.Domain.Entities.Strategies.Core;
//
// namespace TradeFlowGuardian.Strategies.Risk;
//
// public sealed class ATRRiskCalculator(decimal stopAtrMult = 2.0m, decimal takeAtrMult = 2.0m) : IRiskCalculator
// {
//     public string Name => $"ATRRisk({stopAtrMult:F1},{takeAtrMult:F1})";
//
//     // Fill CalculateRisk for backtester compatibility
//     public RiskParameters CalculateRisk(MarketContext context, SignalResult signal)
//     {
//         if (signal.Action is TradeAction.Hold or TradeAction.Exit || context.Candles.Count < 15)
//             return new RiskParameters();
//
//         var atr = ComputeAtr(context.Candles, 14);
//         var px = context.Candles[^1].Close;
//
//         // Guard ultra-low ATR (illiquid/chop)
//         if (atr <= 0) return new RiskParameters();
//
//         decimal sl, tp;
//         if (signal.Action == TradeAction.Buy)
//         {
//             sl = px - atr * stopAtrMult;
//             tp = px + atr * takeAtrMult;
//         }
//         else
//         {
//             sl = px + atr * stopAtrMult;
//             tp = px - atr * takeAtrMult;
//         }
//
//         var rr = atr > 0 ? takeAtrMult / stopAtrMult : 0m;
//         return new RiskParameters(StopLoss: sl, TakeProfit: tp, RiskRewardRatio: rr);
//     }
//
//     public Decision Apply(MarketContext context, Decision decision)
//     {
//         if (decision.Action is TradeAction.Hold or TradeAction.Exit)
//             return decision;
//
//         if (context.Candles.Count < 40)
//             return decision with { Reason = $"{decision.Reason} | Not enough candles for ATR/structure" };
//
//         var n = 14;
//         var atr = ComputeAtr(context.Candles, n);
//         if (atr <= 0) return decision with { Reason = $"{decision.Reason} | ATR<=0" };
//
//         var px = context.Candles[^1].Close;
//
//         var window = 30;
//         var recent = context.Candles.Skip(context.Candles.Count - window - 1).Take(window).ToList();
//         var high = recent.Max(c => c.High);
//         var low = recent.Min(c => c.Low);
//
//         decimal? sl = decision.StopLoss;
//         decimal? tp = decision.TakeProfit;
//
//         if (decision.Action == TradeAction.Buy)
//         {
//             sl ??= Math.Min(px - atr * stopAtrMult, low - 0.5m * atr);
//             tp ??= px + atr * takeAtrMult * 1.05m;
//         }
//         else
//         {
//             sl ??= Math.Max(px + atr * stopAtrMult, high + 0.5m * atr);
//             tp ??= px - atr * takeAtrMult * 1.05m;
//         }
//
//         return decision with { StopLoss = sl, TakeProfit = tp, Reason = $"{decision.Reason} | ATR+Structure SL/TP" };
//     }
//
//     private static decimal ComputeAtr(IReadOnlyList<Candle> candles, int n)
//     {
//         decimal sumTr = 0m;
//         for (int i = candles.Count - n; i < candles.Count; i++)
//         {
//             var cur = candles[i];
//             var prev = candles[i - 1];
//             var tr = Math.Max((double)(cur.High - cur.Low),
//                 Math.Max(Math.Abs((double)(cur.High - prev.Close)),
//                     Math.Abs((double)(cur.Low - prev.Close))));
//             sumTr += (decimal)tr;
//         }
//
//         return sumTr / n;
//     }
// }

// Alternative implementation 
// The difference is that it uses ATRIndicator to calculate ATR and not the one above
// public sealed class ATRRiskCalculator : IRiskCalculator
// {
//     public string Name { get; }
//     private readonly ATRIndicator _atr;
//     private readonly decimal _riskReward;
//     private readonly decimal _stopMultiplier;
//
//     public ATRRiskCalculator(decimal riskReward = 2.0m, decimal stopMultiplier = 1.0m, int atrPeriods = 14)
//     {
//         _atr = new ATRIndicator(atrPeriods);
//         _riskReward = riskReward;
//         _stopMultiplier = stopMultiplier;
//         Name = $"ATRRisk(RR:{riskReward},Stop:{stopMultiplier})";
//     }
//
//     public RiskParameters CalculateRisk(MarketContext context, SignalResult signal)
//     {
//         var atr = context.GetOrCalculate($"{Name}_ATR", 
//             () => _atr.Calculate(context.Candles));
//         
//         var currentPrice = context.Candles.Last().Close;
//         var currentCandle = context.Candles.Last();
//         
//         return signal.Action switch
//         {
//             TradeAction.Buy => new RiskParameters(
//                 StopLoss: Math.Max(currentPrice - (atr * _stopMultiplier), currentCandle.Low),
//                 TakeProfit: currentPrice + ((currentPrice - Math.Max(currentPrice - (atr * _stopMultiplier), currentCandle.Low)) * _riskReward),
//                 RiskRewardRatio: _riskReward
//             ),
//             TradeAction.Sell => new RiskParameters(
//                 StopLoss: Math.Min(currentPrice + (atr * _stopMultiplier), currentCandle.High),
//                 TakeProfit: currentPrice - ((Math.Min(currentPrice + (atr * _stopMultiplier), currentCandle.High) - currentPrice) * _riskReward),
//                 RiskRewardRatio: _riskReward
//             ),
//             _ => new RiskParameters()
//         };
//     }
// }
