// using TradeFlowGuardian.Domain.Entities;
// using TradeFlowGuardian.Strategies.Builder;
// using TradeFlowGuardian.Strategies.Filters;
// using TradeFlowGuardian.Strategies.Risk;
// using TradeFlowGuardian.Strategies.Signals;
//
// namespace TradeFlowGuardian.Strategies.Presets.Trend;
//
// public class TrendPresets
// {
//     public static IStrategy CreateDailyTrendConservative()
//     {
//         return new StrategyBuilder()
//             .WithName("Daily-Trend-Conservative")
//             // Structure break for entry: break above/below recent swing
//             .AddSignal(new BreakoutSignal(50, 0.30m)) // longer lookback for daily structure
//             // Momentum check: RSI neutral window 45-55
//             .AddFilter(new RSIBreakoutFilter(14, 55, 45))
//             // Daily trend: 20 over 50 MA alignment
//             .AddFilter(new MovingAverageCrossFilter(20, 50)) // expects price to align with MA cross direction
//             // Weekly confirmation: price aligned with 100-day MA via higher timeframe
//             .AddFilter(new MultiTimeframeFilter(
//                 "W1",
//                 new TrendFilter(100))) // price above/below 100MA on weekly
//             // Volatility not dead flat
//             .AddFilter(new VolatilityFilter(1.10m))
//             // Trade only London/NY overlap sessions (approx in UTC)
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(7), TimeSpan.FromHours(10)),  // London early
//                     (TimeSpan.FromHours(12), TimeSpan.FromHours(16))  // NY overlap
//                 },
//                 TimeZoneInfo.Utc))
//             // Optional: limit frequency roughly to 2-4 per month (simple cap)
//             .AddFilter(new MaxMonthlyTradesFilter(6))
//             // Risk: SL 3.5x ATR, TP 3R, with partials handled by engine or TP2 logic
//             .WithRiskCalculator(new ATRRiskCalculator(3.5m, 10.5m)) // TP at 3R; partials can be executed by manager
//             .Build();
//     }
// }