// using TradeFlowGuardian.Domain.Entities;
// using TradeFlowGuardian.Strategies.Builder;
// using TradeFlowGuardian.Strategies.Filters;
// using TradeFlowGuardian.Strategies.Risk;
// using TradeFlowGuardian.Strategies.Signals;
//
// namespace TradeFlowGuardian.Strategies.Presets.EMAC;
//
// public static class EmacEurUsdPresets
// {
//     public static IStrategy CreateOptimized()
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
//     // 1. Conservative EMAC - Lower risk, higher quality signals
//     public static IStrategy CreateEmacEurUsdConservative()
//     {
//         return new StrategyBuilder()
//             .WithName("EMAC-EURUSD-Conservative")
//             .AddSignal(new EMACrossoverSignal(10, 20))
//             .AddFilter(new TrendFilter(200))
//             .AddFilter(new RSIFilter(55, 45)) // Tighter RSI range for momentum confirmation
//             .AddFilter(new VolatilityFilter(1.15m)) // Require more volatility to avoid chop
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(8), TimeSpan.FromHours(11)), // London morning
//                     (TimeSpan.FromHours(13), TimeSpan.FromHours(16)) // NY afternoon
//                 },
//                 TimeZoneInfo.Utc
//             ))
//             .AddFilter(new SpreadFilter(1.5m)) // Skip trades if spread > 1.5 pips
//             // .AddFilter(new MaxPositionsFilter(1)) // Only 1 position at a time
//             .WithRiskCalculator(new ATRRiskCalculator(2.5m, 2.0m)) // Wider stop, 2:1 R/R
//             .Build();
//     }
//
// // 2. Aggressive EMAC - More trades, faster signals
//     public static IStrategy CreateEmacEurUsdAggressive()
//     {
//         return new StrategyBuilder()
//             .WithName("EMAC-EURUSD-Aggressive")
//             .AddSignal(new EMACrossoverSignal(7, 18))
//             .AddFilter(new TrendFilter(200))
//             .AddFilter(new RSIFilter(65, 35)) // Wider RSI range for more signals
//             .AddFilter(new VolatilityFilter(1.15m)) // Lower volatility threshold
//             .AddFilter(new NoTradeTimeFilter(
//                 new[]
//                 {
//                     (TimeSpan.FromHours(8), TimeSpan.FromHours(11)), // London session
//                     (TimeSpan.FromHours(13), TimeSpan.FromHours(16)) // NY session
//                 },
//                 TimeZoneInfo.Utc
//             ))
//             .WithRiskCalculator(new ATRRiskCalculator(1.8m, 2.5m)) // Tighter stop, bigger target
//             .Build();
//     }
//
// // // 3. Session-Specific EMAC - London Session Only
// //     public static IStrategy CreateEmacEurUsdLondonSession()
// //     {
// //         return new StrategyBuilder()
// //             .WithName("EMAC-EURUSD-London")
// //             .AddSignal(new EMACrossoverSignal(9, 21))
// //             .AddFilter(new TrendFilter(200))
// //             .AddFilter(new RSIFilter(60, 40))
// //             .AddFilter(new VolatilityFilter(1.12m))
// //             .AddFilter(new NoTradeTimeFilter(
// //                 new[]
// //                 {
// //                     (TimeSpan.FromHours(8), TimeSpan.FromHours(12)) // London session only
// //                 },
// //                 TimeZoneInfo.Utc
// //             ))
// //             .AddFilter(new SessionVolatilityFilter(1.2m)) // Higher ATR during session entry
// //             .AddFilter(new CorrelationFilter(new[] { "GBP_USD", "EUR_GBP" }, 0.7m)) // Avoid correlated trades
// //             .WithRiskCalculator(new ATRRiskCalculator(2.0m, 2.0m))
// //             .Build();
// //     }
//
// // // 4. Multi-Timeframe Confirmation EMAC
// //     public static IStrategy CreateEmacEurUsdMultiTimeframe()
// //     {
// //         return new StrategyBuilder()
// //             .WithName("EMAC-EURUSD-MTF")
// //             .AddSignal(new EMACrossoverSignal(10, 20))
// //             .AddFilter(new TrendFilter(200))
// //             .AddFilter(new RSIFilter(58, 42))
// //             .AddFilter(new VolatilityFilter(1.10m))
// //             .AddFilter(new NoTradeTimeFilter(
// //                 new[]
// //                 {
// //                     (TimeSpan.FromHours(7), TimeSpan.FromHours(11)),
// //                     (TimeSpan.FromHours(13), TimeSpan.FromHours(16))
// //                 },
// //                 TimeZoneInfo.Utc
// //             ))
// //             .AddFilter(new MultiTimeframeFilter("M15", new TrendFilter(50))) // M15 must also be trending
// //             .AddFilter(new PriceActionFilter(3)) // Last 3 candles must show momentum
// //             .WithRiskCalculator(new ATRRiskCalculator(2.2m, 2.0m))
// //             .Build();
// //     }
//
// // // 5. Volatility-Adaptive EMAC - Adjusts to market conditions
// //     public static IStrategy CreateEmacEurUsdAdaptive()
// //     {
// //         return new StrategyBuilder()
// //             .WithName("EMAC-EURUSD-Adaptive")
// //             .AddSignal(new EMACrossoverSignal(9, 21))
// //             .AddFilter(new TrendFilter(200))
// //             .AddFilter(new RSIFilter(60, 40))
// //             .AddFilter(new VolatilityRangeFilter(0.95m, 1.5m)) // Trade only in specific volatility range
// //             .AddFilter(new NoTradeTimeFilter(
// //                 new[]
// //                 {
// //                     (TimeSpan.FromHours(8), TimeSpan.FromHours(11)),
// //                     (TimeSpan.FromHours(13), TimeSpan.FromHours(16))
// //                 },
// //                 TimeZoneInfo.Utc
// //             ))
// //             .AddFilter(new NewsFilter(30)) // Skip trades 30min before/after major news
// //             .AddFilter(new DrawdownFilter(3.0m)) // Pause if account down 3% from high
// //             .WithRiskCalculator(new AdaptiveATRRiskCalculator(2.2m, 1.8m,
// //                 true)) // Adjusts risk based on recent performance
// //             .Build();
// //     }
//
// // // 6. Breakout Enhancement EMAC - Combines trend-following with breakout logic
// //     public static IStrategy CreateEmacEurUsdBreakout()
// //     {
// //         return new StrategyBuilder()
// //             .WithName("EMAC-EURUSD-Breakout")
// //             .AddSignal(new EMACrossoverSignal(9, 21))
// //             .AddFilter(new TrendFilter(200))
// //             .AddFilter(new RSIFilter(60, 40))
// //             .AddFilter(new VolatilityFilter(1.15m)) // Higher volatility for breakouts
// //             .AddFilter(new NoTradeTimeFilter(
// //                 new[]
// //                 {
// //                     (TimeSpan.FromHours(7), TimeSpan.FromHours(11)),
// //                     (TimeSpan.FromHours(13), TimeSpan.FromHours(16))
// //                 },
// //                 TimeZoneInfo.Utc
// //             ))
// //             .AddFilter(new BreakoutConfirmationFilter(20)) // Price must break 20-period high/low
// //             .AddFilter(new VolumeFilter(1.3m)) // Volume must be 30% above average
// //             .WithRiskCalculator(new ATRRiskCalculator(2.5m, 2.5m)) // Wider stops for breakouts
// //             .Build();
// //     }
//
// // // 7. Optimized with Max Daily Trades Limit
// //     public static IStrategy CreateEmacEurUsdDailyLimit()
// //     {
// //         return new StrategyBuilder()
// //             .WithName("EMAC-EURUSD-DailyLimit")
// //             .AddSignal(new EMACrossoverSignal(9, 21))
// //             .AddFilter(new TrendFilter(200))
// //             .AddFilter(new RSIFilter(60, 40))
// //             .AddFilter(new VolatilityFilter(1.10m))
// //             .AddFilter(new NoTradeTimeFilter(
// //                 new[]
// //                 {
// //                     (TimeSpan.FromHours(7), TimeSpan.FromHours(11)),
// //                     (TimeSpan.FromHours(12.5), TimeSpan.FromHours(16))
// //                 },
// //                 TimeZoneInfo.FindSystemTimeZoneById("Europe/London")
// //             ))
// //             .AddFilter(new MaxDailyTradesFilter(3)) // Max 3 trades per day
// //             .AddFilter(new MinTimeBetweenTradesFilter(TimeSpan.FromMinutes(30))) // 30min between trades
// //             .WithRiskCalculator(new ATRRiskCalculator(2.2m, 1.8m))
// //             .Build();
// //     }
//
// // // 8. Mean Reversion Complement (for ranging markets)
// //     public static IStrategy CreateMeanReversionEurUsd()
// //     {
// //         return new StrategyBuilder()
// //             .WithName("MeanReversion-EURUSD")
// //             .AddSignal(new BollingerBandReversal(20, 2.0m)) // Price touches outer band
// //             .AddFilter(new TrendFilter(200, inverse: true)) // Price near EMA200 (ranging)
// //             .AddFilter(new RSIFilter(30, 70, reversal: true)) // RSI oversold/overbought
// //             .AddFilter(new VolatilityFilter(0.95m, max: 1.2m)) // Low to moderate volatility
// //             .AddFilter(new NoTradeTimeFilter(
// //                 new[]
// //                 {
// //                     (TimeSpan.FromHours(8), TimeSpan.FromHours(11)),
// //                     (TimeSpan.FromHours(13), TimeSpan.FromHours(16))
// //                 },
// //                 TimeZoneInfo.Utc
// //             ))
// //             .AddFilter(new ADXFilter(20, inverse: true)) // ADX < 20 (weak trend)
// //             .WithRiskCalculator(new ATRRiskCalculator(1.5m, 1.5m)) // Quick in and out, 1:1 R/R
// //             .Build();
// //     }
// }
