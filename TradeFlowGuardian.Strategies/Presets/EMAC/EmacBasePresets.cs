// using TradeFlowGuardian.Domain.Entities;
// using TradeFlowGuardian.Strategies.Builder;
// using TradeFlowGuardian.Strategies.Filters;
// using TradeFlowGuardian.Strategies.Risk;
// using TradeFlowGuardian.Strategies.Signals;
//
// namespace TradeFlowGuardian.Strategies.Presets.EMAC;
//
// public static class EmacPresets
// {
//     public static IStrategy CreateEmacStrategy()
//     {
//         return new StrategyBuilder()
//             .WithName("EMAC-Composable")
//             // replace SMA crossover with EMA crossover for earlier trend entries
//             .AddSignal(new EMACrossoverSignal(9, 21))
//             .AddFilter(new TrendFilter(200))
//             .AddFilter(new RSIFilter(55, 45))
//             .AddFilter(new VolatilityFilter(0.95m))
//             // Session timing(example: London + NY overlap in Europe / London time)
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(7), TimeSpan.FromHours(11)), // London morning
//                     (TimeSpan.FromHours(12.5), TimeSpan.FromHours(16)) // NY overlap approx 12:30–16:00 London time
//                 },
//                 TimeZoneInfo.FindSystemTimeZoneById("Europe/London")
//             ))
//             .WithRiskCalculator(new ATRRiskCalculator(2.0m, 1.0m))
//             .Build();
//     }
// }