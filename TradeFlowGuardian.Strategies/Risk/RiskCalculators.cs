//
// using TradeFlowGuardian.Domain.Entities;
// using TradeFlowGuardian.Domain.Entities.Strategies.Core;
// using TradeFlowGuardian.Strategies.Indicators;
//
// namespace TradeFlowGuardian.Strategies.Risk;
//
//
// public sealed class PercentageRiskCalculator : IRiskCalculator
// {
//     public string Name { get; }
//     private readonly decimal _stopPercent;
//     private readonly decimal _riskReward;
//
//     public PercentageRiskCalculator(decimal stopPercent = 0.01m, decimal riskReward = 2.0m)
//     {
//         _stopPercent = stopPercent;
//         _riskReward = riskReward;
//         Name = $"PercentRisk({stopPercent * 100:F1}%,RR:{riskReward})";
//     }
//
//     public RiskParameters CalculateRisk(MarketContext context, SignalResult signal)
//     {
//         var currentPrice = context.Candles.Last().Close;
//         
//         return signal.Action switch
//         {
//             TradeAction.Buy => new RiskParameters(
//                 StopLoss: currentPrice * (1 - _stopPercent),
//                 TakeProfit: currentPrice + ((currentPrice - currentPrice * (1 - _stopPercent)) * _riskReward),
//                 RiskRewardRatio: _riskReward
//             ),
//             TradeAction.Sell => new RiskParameters(
//                 StopLoss: currentPrice * (1 + _stopPercent),
//                 TakeProfit: currentPrice - ((currentPrice * (1 + _stopPercent) - currentPrice) * _riskReward),
//                 RiskRewardRatio: _riskReward
//             ),
//             _ => new RiskParameters()
//         };
//     }
// }
