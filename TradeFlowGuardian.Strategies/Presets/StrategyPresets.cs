// using TradeFlowGuardian.Domain.Entities;
// using TradeFlowGuardian.Strategies.Builder;
// using TradeFlowGuardian.Strategies.Signals;
// using TradeFlowGuardian.Strategies.Filters;
// using TradeFlowGuardian.Strategies.Filters.Old;
// using TradeFlowGuardian.Strategies.Risk;
// using TradeFlowGuardian.Strategies.Presets.EMAC;
//
// namespace TradeFlowGuardian.Strategies.Presets;
//
// public static class StrategyPresets
// {
//     
//     public static IStrategy CreateEmacStrategy()
//     {
//         return EmacPresets.CreateEmacStrategy();
//     }
//
//     public static IStrategy CreateEmacEurUsdConservative()
//     {
//         return EmacEurUsdPresets.CreateEmacEurUsdConservative();
//     }
//
//     public static IStrategy CreateEmacEurUsdAggressive()
//     {
//         return EmacEurUsdPresets.CreateEmacEurUsdAggressive();
//     }
//     
//     public static IStrategy CreateBreakoutStrategy()
//     {
//         return new StrategyBuilder()
//             .WithName("Breakout-Momentum")
//             .AddSignal(new BreakoutSignal(5))
//             .AddFilter(new RSIFilter(55, 45))
//             .AddFilter(new VolatilityFilter(1.2m))
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(7), TimeSpan.FromHours(11)),
//                     (TimeSpan.FromHours(12.5), TimeSpan.FromHours(16))
//                 },
//                 TimeZoneInfo.FindSystemTimeZoneById("Europe/London")
//             ))
//             .WithRiskCalculator(new ATRRiskCalculator(1.5m, 0.8m))
//             .Build();
//     }
//
//     public static IStrategy CreateConservativeStrategy()
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
//     public static IStrategy CreateAggressiveStrategy()
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
//
//     public static IStrategy CreateMeanReversionStrategy()
//     {
//         return new StrategyBuilder()
//             .WithName("RSI-MeanReversion")
//             .AddSignal(new RSIReversionSignal(period: 14, overbought: 70m, oversold: 30m))
//             .AddFilter(new VolatilityFilter(0.9m))
//             .AddFilter(new DayOfWeekFilter(new[] { DayOfWeek.Monday, DayOfWeek.Friday })) // example bias
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(0), TimeSpan.FromHours(6)), // Asia quiet hours example
//                     (TimeSpan.FromHours(18), TimeSpan.FromHours(23)) // Late NY
//                 },
//                 TimeZoneInfo.Utc
//             ))
//             .WithRiskCalculator(new ATRRiskCalculator(2.2m, 1.2m))
//             .Build();
//     }
//
//     public static IStrategy CreateEmacEurUsdOptimized()
//     {
//         return new StrategyBuilder()
//             .WithName("EMAC-EURUSD-Optimized")
//             .AddSignal(new EMACrossoverSignal(9, 21))
//             .AddFilter(new TrendFilter(200)) // classic trend anchor
//             .AddFilter(new RSIFilter(60, 40)) // was 58/42: stricter centerline for less chop
//             .AddFilter(new VolatilityFilter(1.10m)) // was 1.05m: require stronger expansion
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(7), TimeSpan.FromHours(11)),
//                     (TimeSpan.FromHours(12.5), TimeSpan.FromHours(16))
//                 },
//                 TimeZoneInfo.FindSystemTimeZoneById("Europe/London")
//             ))
//             .WithRiskCalculator(new ATRRiskCalculator(2.2m, 1.8m)) // slightly tighter stop, higher TP multiple for PF
//             .Build();
//     }
//
//     public static IStrategy CreateEmacEurUsdPullback()
//     {
//         return new StrategyBuilder()
//             .WithName("EMAC-EURUSD-Pullback")
//             .AddSignal(new EMACrossoverSignal(9, 21))
//             .AddFilter(new TrendFilter(200))
//             .AddFilter(new RSIFilter(60, 40)) // stronger trend bias
//             .AddFilter(new VolatilityFilter(1.05m)) // was 1.10m: allow slightly more movement-qualified trades
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(7), TimeSpan.FromHours(11)), // widened back to standard London
//                     (TimeSpan.FromHours(12.5), TimeSpan.FromHours(16)) // NY overlap
//                 },
//                 TimeZoneInfo.FindSystemTimeZoneById("Europe/London")
//             ))
//             .WithRiskCalculator(new ATRRiskCalculator(2.5m, 1.6m)) // wider stop + higher RR to lift PF
//             .Build();
//     }
//
//     public static IStrategy CreateEmacEurUsdSlow()
//     {
//         return new StrategyBuilder()
//             .WithName("EMAC-EURUSD-Slow")
//             .AddSignal(new EMACrossoverSignal(10, 26)) // was 12/26: a touch faster to avoid missing moves
//             .AddFilter(new TrendFilter(200))
//             .AddFilter(new RSIFilter(55, 45))
//             .AddFilter(new VolatilityFilter(0.98m)) // was 1.00: allow slightly more setups
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(7), TimeSpan.FromHours(11)),
//                     (TimeSpan.FromHours(12.5), TimeSpan.FromHours(16))
//                 },
//                 TimeZoneInfo.FindSystemTimeZoneById("Europe/London")
//             ))
//             .WithRiskCalculator(new ATRRiskCalculator(2.2m, 1.4m))
//             .Build();
//     }
//
//     public static IStrategy CreateBreakoutSessionStrategy()
//     {
//         return new StrategyBuilder()
//             .WithName("Breakout-Session")
//             .AddSignal(new BreakoutSignal(10)) // longer lookback for cleaner breaks
//             .AddFilter(new VolatilityFilter(1.30m)) // only act in higher vol
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(7), TimeSpan.FromHours(11)),
//                     (TimeSpan.FromHours(12.5), TimeSpan.FromHours(16))
//                 },
//                 TimeZoneInfo.FindSystemTimeZoneById("Europe/London")
//             ))
//             .WithRiskCalculator(new ATRRiskCalculator(1.8m, 0.9m))
//             .Build();
//     }
//
//     public static IStrategy CreateBreakoutSessionLondonOnly()
//     {
//         return new StrategyBuilder()
//             .WithName("Breakout-Session-LondonOnly")
//             .AddSignal(new BreakoutSignal(12)) // even stricter structure
//             .AddFilter(new VolatilityFilter(1.35m)) // high-vol only
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(7), TimeSpan.FromHours(10.5)) // London morning only
//                 },
//                 TimeZoneInfo.FindSystemTimeZoneById("Europe/London")
//             ))
//             .WithRiskCalculator(new ATRRiskCalculator(2.0m, 1.0m))
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
// }
