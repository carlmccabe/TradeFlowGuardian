// using TradeFlowGuardian.Domain.Entities;
// using TradeFlowGuardian.Strategies.Builder;
// using TradeFlowGuardian.Strategies.Filters;
// using TradeFlowGuardian.Strategies.Filters.Old;
// using TradeFlowGuardian.Strategies.Risk;
// using TradeFlowGuardian.Strategies.Signals;
//
// namespace TradeFlowGuardian.Strategies.Presets.MeanReversion;
//
// public static class MeanReversionPresets
// {
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
// }