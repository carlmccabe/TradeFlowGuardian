// using TradeFlowGuardian.Domain.Entities;
// using TradeFlowGuardian.Strategies.Builder;
// using TradeFlowGuardian.Strategies.Filters;
// using TradeFlowGuardian.Strategies.Risk;
// using TradeFlowGuardian.Strategies.Signals;
//
// namespace TradeFlowGuardian.Strategies.Presets.Breakout;
//
// /// <summary>
// /// Presets for Breakout + RSI strategies with optional confirmations.
// /// </summary>
// public static class BreakoutStrategyPresets
// {
//     public static IStrategy CreateBreakoutRSIOriginal()
//     {
//         return new StrategyBuilder()
//             .WithName("Breakout-RSI-Original")
//             .AddSignal(new BreakoutSignal(30))
//             .AddFilter(new RSIBreakoutFilter(14, 55, 42))
//             .AddFilter(new ShortTermTrendFilter(5, 3))
//             .WithRiskCalculator(new ATRRiskCalculator(2.2m, 2.0m))
//             .Build();
//     }
//
//     public static IStrategy CreateBreakoutRSIEnhanced()
//     {
//         return new StrategyBuilder()
//             .WithName("Breakout-RSI-Enhanced")
//             .AddSignal(new BreakoutSignal(30))
//             .AddFilter(new RSIBreakoutFilter(14, 55, 45))
//             .AddFilter(new ShortTermTrendFilter(5, 3))
//             .AddFilter(new TrendFilter(200))
//             .AddFilter(new VolatilityFilter(1.15m))
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(8), TimeSpan.FromHours(11)),
//                     (TimeSpan.FromHours(13), TimeSpan.FromHours(16))
//                 },
//                 TimeZoneInfo.Utc))
//             .AddFilter(new SpreadFilter(1.5m))
//             .WithRiskCalculator(new ATRRiskCalculator(1.4m, 2.8m))
//             .Build();
//     }
//
//     public static IStrategy CreateBreakoutRSIConservative()
//     {
//         return new StrategyBuilder()
//             .WithName("Breakout-RSI-Conservative")
//             .AddSignal(new BreakoutSignal(40, 0.35m))
//             .AddFilter(new RSIBreakoutFilter(14, 62, 38))
//             .AddFilter(new ShortTermTrendFilter(8, 5))
//             .AddFilter(new VolatilityFilter(1.15m))
//             .AddFilter(new AdxFilter(25))
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(8), TimeSpan.FromHours(11)),
//                     (TimeSpan.FromHours(13), TimeSpan.FromHours(16))
//                 },
//                 TimeZoneInfo.Utc))
//             // .AddFilter(new MaxDailyTradesFilter(2))
//             .WithRiskCalculator(new ATRRiskCalculator(1.8m, 2.2m))
//             .Build();
//     }
//
//     public static IStrategy CreateBreakoutRSIAggressive()
//     {
//         return new StrategyBuilder()
//             .WithName("Breakout-RSI-Aggressive")
//             .AddSignal(new BreakoutSignal(22, 0.25m))
//             .AddFilter(new RSIBreakoutFilter(9, 56, 44))
//             .AddFilter(new ShortTermTrendFilter(3, 2))
//             .AddFilter(new AdxFilter(18))
//             .AddFilter(new VolatilityFilter(1.05m))
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(7), TimeSpan.FromHours(12)),
//                     (TimeSpan.FromHours(13), TimeSpan.FromHours(17))
//                 },
//                 TimeZoneInfo.Utc))
//             .WithRiskCalculator(new ATRRiskCalculator(1.0m, 2.0m))
//             .Build();
//     }
//
//     // public static IStrategy CreateBreakoutRSIMultiTimeframe()
//     // {
//     //     return new StrategyBuilder()
//     //         .WithName("Breakout-RSI-MTF")
//     //         .AddSignal(new BreakoutSignal(20))
//     //         .AddFilter(new RSIBreakoutFilter(14, 50, 50))
//     //         .AddFilter(new ShortTermTrendFilter(5, 3))
//     //         .AddFilter(new TrendFilter(200))
//     //         // .AddFilter(new MultiTimeframeFilter("M15", new TrendFilter(50)))
//     //         // .AddFilter(new MultiTimeframeFilter("M15", new RSIBreakoutFilter(14, 50, 50)))
//     //         .AddFilter(new VolatilityFilter(1.10m))
//     //         .AddFilter(new NoTradeTimeFilter(
//     //             new[]
//     //             {
//     //                 (TimeSpan.FromHours(8), TimeSpan.FromHours(11)),
//     //                 (TimeSpan.FromHours(13), TimeSpan.FromHours(16))
//     //             },
//     //             TimeZoneInfo.Utc))
//     //         .WithRiskCalculator(new ATRRiskCalculator(2.2m, 2.0m))
//     //         .Build();
//     // }
// }
