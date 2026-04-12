using TradeFlowGuardian.Strategies.Indicators;
using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;

namespace TradeFlowGuardian.Strategies.Filters;

// public sealed class TrendFilter : IFilter
// {
//     public string Name { get; }
//     private readonly EmaIndicator _trendEma;
//
//     public TrendFilter(int periods = 200)
//     {
//         _trendEma = new EmaIndicator(periods);
//         Name = $"TrendFilter({periods})";
//     }
//
//     public bool ShouldAllow(MarketContext context, SignalResult signal)
//     {
//         var ema = context.GetOrCalculate($"{Name}_EMA", 
//             () => _trendEma.Calculate(context.Candles));
//         
//         var currentPrice = context.Candles.Last().Close;
//         
//         return signal.Action switch
//         {
//             TradeAction.Buy => currentPrice > ema,  // Only buy above trend
//             TradeAction.Sell => currentPrice < ema, // Only sell below trend
//             _ => true
//         };
//     }
// }
//
// public sealed class RSIFilter : IFilter
// {
//     public string Name { get; }
//     private readonly RsiIndicator _rsi;
//     private readonly decimal _longMin;
//     private readonly decimal _shortMax;
//
//     public RSIFilter(decimal longMin = 55m, decimal shortMax = 45m, int periods = 14)
//     {
//         _rsi = new RsiIndicator(periods);
//         _longMin = longMin;
//         _shortMax = shortMax;
//         Name = $"RSIFilter({longMin},{shortMax})";
//     }
//
//     public bool ShouldAllow(MarketContext context, SignalResult signal)
//     {
//         var rsi = context.GetOrCalculate($"{Name}_RSI", 
//             () => _rsi.Calculate(context.Candles));
//         
//         return signal.Action switch
//         {
//             TradeAction.Buy => rsi > _longMin,
//             TradeAction.Sell => rsi < _shortMax,
//             _ => true
//         };
//     }
// }
