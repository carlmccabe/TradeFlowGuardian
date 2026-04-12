// using TradeFlowGuardian.Domain.Entities;
// using TradeFlowGuardian.Strategies.Builder;
// using TradeFlowGuardian.Strategies.Filters;
// using TradeFlowGuardian.Strategies.Filters.Old;
// using TradeFlowGuardian.Strategies.Risk;
// using TradeFlowGuardian.Strategies.Signals;
//
// namespace TradeFlowGuardian.Strategies.Presets.RiskProfiles;
//
// public static class RiskProfilePresets
// {
//     public static IStrategy CreateConservative()
//     {
//         return new StrategyBuilder()
//             .WithName("Conservative-Trend")
//             .AddSignal(new SMACrossoverSignal(21, 50))
//             .AddFilter(new TrendFilter(200))
//             .AddFilter(new RSIFilter(60, 40))
//             .AddFilter(new VolatilityFilter(0.8m))
//             .AddFilter(new DayOfWeekFilter(new[] { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday }))
//             .WithRiskCalculator(new ATRRiskCalculator(3.0m, 1.5m))
//             .Build();
//     }
//
//     public static IStrategy CreateAggressiveScalpSafe()
//     {
//         return new StrategyBuilder()
//             .WithName("Aggressive-Scalp-Safe")
//             .AddSignal(new EMACrossoverSignal(8, 20)) // slightly slower than 5/13
//             .AddSignal(new BreakoutSignal(4))
//             .AddFilter(new VolatilityFilter(1.30m)) // require strong movement
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(7.5), TimeSpan.FromHours(10)), // shorter high-liquidity window
//                     (TimeSpan.FromHours(13), TimeSpan.FromHours(15.5))
//                 },
//                 TimeZoneInfo.FindSystemTimeZoneById("Europe/London")
//             ))
//             .WithRiskCalculator(new ATRRiskCalculator(1.2m, 0.7m)) // tighter, safer scalp profile
//             .Build();
//     }
//
//     public static IStrategy CreateAggressive()
//     {
//         return new StrategyBuilder()
//             .WithName("Aggressive-Scalp")
//             // use EMA crossover for speed plus breakout for momentum burst
//             .AddSignal(new EMACrossoverSignal(5, 13))
//             .AddSignal(new BreakoutSignal(3))
//             // relax volatility to allow trades, expand session so it's not empty
//             .AddFilter(new VolatilityFilter(1.1m))
//             // .AddFilter(new NoTradeTimeFilter(
//             //     new[]
//             //     {
//             //         (TimeSpan.FromHours(7), TimeSpan.FromHours(12)), // London session wider
//             //         (TimeSpan.FromHours(12.5), TimeSpan.FromHours(16)) // NY overlap
//             //     },
//             //     TimeZoneInfo.FindSystemTimeZoneById("Europe/London")
//             // ))
//             .WithRiskCalculator(new ATRRiskCalculator(1.0m, 0.5m))
//             .Build();
//     }
// }