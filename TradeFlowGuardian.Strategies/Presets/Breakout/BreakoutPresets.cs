// using TradeFlowGuardian.Domain.Entities;
// using TradeFlowGuardian.Strategies.Builder;
// using TradeFlowGuardian.Strategies.Filters;
// using TradeFlowGuardian.Strategies.Filters.Composite;
// using TradeFlowGuardian.Strategies.Risk;
// using TradeFlowGuardian.Strategies.Signals;
//
// namespace TradeFlowGuardian.Strategies.Presets.Breakout;
//
// public static class BreakoutPresets
// {
//     public static IStrategy CreateBreakoutStrategy()
//     {
//         return new StrategyBuilder()
//             .WithName("Breakout-Momentum")
//             .AddSignal(new BreakoutSignal(5))
//             .AddFilter(new RSIFilter(55, 45))
//             .AddFilter(new VolatilityFilter(1.2m))
//             .WithRiskCalculator(new ATRRiskCalculator(1.5m, 0.8m))
//             .Build();
//     }
//
//     public static IStrategy CreateBreakoutSession()
//     {
//         return new StrategyBuilder()
//             .WithName("Breakout-Session")
//             .AddSignal(new BreakoutSignal(10)) // longer lookback for cleaner breaks
//             .AddFilter(new VolatilityFilter(1.30m)) // only act in higher vol
//             .AddFilter(new TimeFilter(
//                 "Breakout-Momentum-Time", 
//                 TimeSpan.FromHours(9), 
//                 TimeSpan.FromHours(11), 
//                 "Europe/London"))
//             .AddFilter(new TimeFilter(
//                 "Breakout-Momentum-Time", 
//                 TimeSpan.FromHours(12.5), 
//                 TimeSpan.FromHours(16),
//                 "Europe/London")
//             )
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(7), TimeSpan.FromHours(11)),
//                     (TimeSpan.FromHours(12.5), TimeSpan.FromHours(16))
//                 },
//                 TimeZoneInfo.FindSystemTimeZoneById("Europe/London")
//             ))
//
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
//             .AddFilter(new TimeFilter(
//                 "LondonMorning",
//                 TimeSpan.FromHours(7),
//                 TimeSpan.FromHours(10.5),
//                 "Europe/London")
//             )
//             .WithRiskCalculator(new ATRRiskCalculator(2.0m, 1.0m))
//             .Build();
//     }
// }