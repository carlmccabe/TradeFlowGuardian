using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Filters.Base;
using TradeFlowGuardian.Strategies.Indicators;

namespace TradeFlowGuardian.Strategies.Filters;

/// <summary>
/// Volatility Filter that gates trades based on whether current volatility (ATR) exceeds a dynamic threshold.
/// </summary>
/// <remarks>
/// <para>
/// 📚 What Does This Filter Do?
/// </para>
/// <para>
/// The Volatility Filter prevents trading during "dead" market periods when volatility is abnormally low.
/// It compares the current ATR (Average True Range) to a smoothed ATR baseline (EMA of recent ATR values).
/// If current volatility is too low relative to the recent average, the filter blocks trades.
/// </para>
/// <para>
/// Think of it as asking: "Is the market moving enough to justify a trade?" Low volatility markets are:
/// - Harder to profit from (small moves)
/// - More susceptible to spread costs eating your edge
/// - Often choppy with frequent false signals
/// - Risky for stop-loss placement (tight stops get hit, wide stops risk too much)
/// </para>
/// <para>
/// 🎯 Purpose and Use Cases:
/// </para>
/// <list type="bullet">
///   <item><description>Avoid Dead Markets: Don't trade during lunch hours, holidays, or pre-news lulls when nothing moves</description></item>
///   <item><description>Spread Protection: When ATR is tiny, broker spread becomes huge % of potential profit</description></item>
///   <item><description>Stop-Loss Validity: Low volatility makes stop placement impossible (too tight = stopped out, too wide = risk too much)</description></item>
///   <item><description>Opportunity Cost: Why tie up capital in dead market when you could wait for active period?</description></item>
///   <item><description>Dynamic Threshold: Uses recent ATR average, not fixed value - adapts to changing market conditions</description></item>
/// </list>
/// <para>
/// 🔢 How It Works (Algorithm):
/// </para>
/// <code>
/// Step 1: Calculate ATR (Wilder's smoothed volatility) over recent candles
/// 
/// Step 2: Collect last N ATR values (history lookback, e.g., 50)
///   - Skip nulls (warm-up period)
///   - Build time series of volatility measurements
/// 
/// Step 3: Smooth ATR history with EMA (e.g., 20-period)
///   - Creates "ATR Floor" = baseline of recent average volatility
///   - EMA adapts faster than SMA to changing market regime
/// 
/// Step 4: Compare current ATR to ATR Floor
///   - If CurrentATR > ATRFloor × Threshold: PASS (sufficient volatility)
///   - If CurrentATR ≤ ATRFloor × Threshold: BLOCK (too quiet)
/// 
/// Example:
/// - Recent ATR values: 40, 42, 38, 41, 39 pips (average ~40)
/// - ATR Floor (EMA): 40 pips
/// - Threshold: 0.95 (require 95% of recent average)
/// - Current ATR: 35 pips
/// - Decision: 35 &lt; 40 × 0.95 (38) → BLOCK trade
/// 
/// Why it works:
/// - During normal volatility: CurrentATR ≈ ATRFloor → trades allowed
/// - During volatility spike: CurrentATR > ATRFloor → trades allowed (opportunity!)
/// - During dead period: CurrentATR &lt;&lt; ATRFloor → trades blocked (avoid whipsaw)
/// </code>
/// <para>
/// 💡 Key Insight: This is a RELATIVE filter, not absolute. It compares current volatility to recent history,
/// so it automatically adapts when market regime changes. EUR/USD "low volatility" is different from GBP/JPY "low volatility."
/// </para>
/// <para>
/// ⚙️ Parameters Explained:
/// </para>
/// <list type="bullet">
///   <item><description>Threshold (default: 0.95 = 95%): Current ATR must be >= this % of ATR Floor
///     <para>- 1.0 (100%): Only trade when volatility is at or above recent average</para>
///     <para>- 0.95 (95%): Allow slightly below average (recommended - some tolerance)</para>
///     <para>- 0.90 (90%): More permissive, only blocks very dead markets</para>
///     <para>- 0.80 (80%): Very permissive, rarely blocks trades</para>
///     <para>Lower threshold = more trades but during suboptimal volatility</para>
///   </description></item>
///   <item><description>ATR Period (default: 14): Period for Wilder's ATR calculation
///     <para>- Standard: 14 (Wilder's original, industry standard)</para>
///     <para>- Shorter (7-10): More responsive to recent volatility changes</para>
///     <para>- Longer (20-30): Smoother, less reactive</para>
///   </description></item>
///   <item><description>History Lookback (default: 50): How many recent candles to consider for ATR baseline
///     <para>- Shorter (20-30): Adapts quickly to regime changes, but less stable</para>
///     <para>- Standard (50): Good balance, represents ~2-3 days on H1, ~10 days on D1</para>
///     <para>- Longer (100+): Very stable baseline, slow to adapt to new regime</para>
///   </description></item>
///   <item><description>ATR EMA Period (default: 20): Smoothing period for ATR Floor calculation
///     <para>- Shorter (10-15): ATR Floor adapts faster to changing volatility</para>
///     <para>- Standard (20): Balanced smoothing</para>
///     <para>- Longer (30-50): Very smooth, only major regime changes affect it</para>
///   </description></item>
/// </list>
/// <para>
/// 🎬 Real-World Examples:
/// </para>
/// <example>
/// <code>
/// // Example 1: Basic volatility filter (default settings)
/// // Only trade when market is moving at least 95% of recent average
/// var volFilter = new VolatilityFilter(
///     threshold: 0.95m,      // Require 95% of recent average ATR
///     atrPeriod: 14,         // Standard Wilder's ATR
///     historyLookback: 50,   // Look at last 50 candles
///     atrEmaPeriod: 20       // Smooth with 20-period EMA
/// );
/// 
/// // Typical result: Blocks trades during lunch hours, weekends, pre-holiday lulls
/// 
/// // Example 2: Strict volatility requirement (only trade hot markets)
/// var strictVolFilter = new VolatilityFilter(
///     threshold: 1.0m,       // Require 100% of average (no tolerance)
///     atrPeriod: 14,
///     historyLookback: 50,
///     atrEmaPeriod: 20
/// );
/// 
/// // Blocks more trades, but those that pass have good volatility
/// // Good for strategies that need movement (breakouts, momentum)
/// 
/// // Example 3: Lenient filter (only block truly dead markets)
/// var lenientVolFilter = new VolatilityFilter(
///     threshold: 0.80m,      // Allow down to 80% of average
///     atrPeriod: 14,
///     historyLookback: 50,
///     atrEmaPeriod: 20
/// );
/// 
/// // More permissive, rarely blocks - use when your strategy works in low vol
/// 
/// // Example 4: Fast-adapting filter (for changing market regimes)
/// var adaptiveVolFilter = new VolatilityFilter(
///     threshold: 0.95m,
///     atrPeriod: 10,         // Faster ATR response
///     historyLookback: 30,   // Shorter history window
///     atrEmaPeriod: 15       // Faster EMA smoothing
/// );
/// 
/// // Adapts quickly when volatility regime changes
/// // Good for markets that transition between high/low vol frequently
/// 
/// // Example 5: In a composite filter with other conditions
/// var entryConditions = new AndFilter(
///     "complete_entry",
///     new VolatilityFilter(0.95m),                    // Sufficient volatility
///     new AdxFilter("adx_25", 25m),                   // Trending market
///     new RsiThresholdFilter("rsi_not_extreme", 70m, inverse: true),  // Not overbought
///     new TimeFilter("trading_hours", TimeSpan.FromHours(8), TimeSpan.FromHours(16))
/// );
/// 
/// // Only enter when ALL conditions met:
/// // 1. Market is moving enough (volatility)
/// // 2. There's a trend to follow (ADX)
/// // 3. Not chasing at extreme (RSI)
/// // 4. During active trading hours (Time)
/// </code>
/// </example>
/// <para>
/// ⚠️ Common Mistakes and Solutions:
/// </para>
/// <list type="bullet">
///   <item><description>❌ Mistake: Using fixed ATR threshold (e.g., "block if ATR &lt; 20 pips")
///     <para>Problem: What's "low" for EUR/USD is "extremely low" for GBP/JPY. Fixed threshold doesn't adapt.</para>
///     <para>✅ Solution: This filter uses relative comparison (% of recent average), adapts to each instrument.</para>
///   </description></item>
///   <item><description>❌ Mistake: Setting threshold too high (like 1.1 or 1.2)
///     <para>Problem: Only trades during volatility spikes, misses normal good conditions.</para>
///     <para>✅ Solution: Keep threshold 0.90-1.0. You want to block dead markets, not require exceptional volatility.</para>
///   </description></item>
///   <item><description>❌ Mistake: History lookback too short (like 10 candles)
///     <para>Problem: ATR Floor whipsaws, not stable baseline. Filter becomes unreliable.</para>
///     <para>✅ Solution: Use minimum 30, preferably 50+ candles for stable baseline.</para>
///   </description></item>
///   <item><description>❌ Mistake: Not combining with other filters
///     <para>Problem: Volatility alone doesn't mean good conditions. High volatility + ranging market = whipsaw.</para>
///     <para>✅ Solution: Combine with ADX (trend strength), time filter (active hours), trend direction filter.</para>
///   </description></item>
///   <item><description>❌ Mistake: Using this filter for mean-reversion strategies
///     <para>Problem: Mean-reversion profits FROM low volatility ranges. This filter blocks those opportunities!</para>
///     <para>✅ Solution: Only use for trend-following/breakout strategies. Mean-reversion needs inverse logic.</para>
///   </description></item>
/// </list>
/// <para>
/// 🎓 Tuning Guidelines by Strategy Type:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Strategy Type</term>
///     <description>Threshold</description>
///     <description>ATR Period</description>
///     <description>Why?</description>
///   </listheader>
///   <item>
///     <term>Breakout Trading</term>
///     <description>0.95-1.0</description>
///     <description>14</description>
///     <description>Need good volatility for breakout to follow through</description>
///   </item>
///   <item>
///     <term>Trend Following</term>
///     <description>0.90-0.95</description>
///     <description>14</description>
///     <description>Some tolerance okay, trends work in moderate volatility</description>
///   </item>
///   <item>
///     <term>Scalping</term>
///     <description>1.0-1.1</description>
///     <description>10</description>
///     <description>Need high volatility for quick moves, spread protection critical</description>
///   </item>
///   <item>
///     <term>Momentum Trading</term>
///     <description>0.95-1.0</description>
///     <description>10-14</description>
///     <description>Momentum requires movement, not dead markets</description>
///   </item>
///   <item>
///     <term>Mean Reversion</term>
///     <description>N/A - DON'T USE</description>
///     <description>-</description>
///     <description>Mean reversion profits from LOW volatility - this filter counterproductive</description>
///   </item>
/// </list>
/// <para>
/// 🏆 Best Practices:
/// </para>
/// <list type="number">
///   <item><description>Start with default settings (0.95, 14, 50, 20) and adjust based on backtest results</description></item>
///   <item><description>Monitor blocked trades: If filter blocks 80%+ of signals, threshold is too high</description></item>
///   <item><description>Combine with time filter: Volatility naturally cycles with market hours, reinforce with time-based filter</description></item>
///   <item><description>Different thresholds for different sessions: Asian session might need lower threshold than London/NY overlap</description></item>
///   <item><description>Log diagnostics: Track CurrentATR and ATRFloor values to understand why trades were blocked</description></item>
///   <item><description>Backtest impact: Measure win rate, profit factor, max drawdown WITH and WITHOUT filter to quantify value</description></item>
/// </list>
/// <para>
/// 📊 Expected Impact on Trading Results:
/// </para>
/// <list type="bullet">
///   <item><description>Trade Frequency: Reduces trades by 20-40% (blocks dead periods)</description></item>
///   <item><description>Win Rate: May increase 5-15% (avoiding poor conditions)</description></item>
///   <item><description>Average Win: Should stay similar (same strategy in better conditions)</description></item>
///   <item><description>Max Drawdown: Often reduces by 10-25% (avoids choppy, whipsaw periods)</description></item>
///   <item><description>Profit Factor: Usually improves because you're trading during better market regimes</description></item>
/// </list>
/// <para>
/// 💰 Spread Cost Consideration:
/// </para>
/// <para>
/// One critical but often overlooked benefit: spread protection. When ATR is 20 pips and spread is 1 pip,
/// spread costs 5% of potential move. When ATR drops to 10 pips, spread costs 10%! This filter prevents
/// trading when spread becomes disproportionately expensive relative to expected move.
/// </para>
/// <para>
/// Example calculation:
/// </para>
/// <code>
/// EUR/USD spread: 1 pip
/// Current ATR: 30 pips
/// Spread as % of ATR: 1/30 = 3.3% (acceptable)
/// 
/// During dead period:
/// Current ATR: 10 pips
/// Spread as % of ATR: 1/10 = 10% (too expensive!)
/// 
/// Volatility Filter at 0.95 threshold:
/// If ATR drops below 95% of recent 40-pip average (38 pips), it blocks trades.
/// This protects you from periods when spread eats too much of potential profit.
/// </code>
/// </remarks>
public sealed class VolatilityFilter : FilterBase
{
    private readonly AtrIndicator _atrIndicator;
    private readonly EmaIndicator _emaIndicatorForAtr;
    private readonly int _atrPeriod;
    private readonly int _historyLookback;
    private readonly decimal _threshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatilityFilter"/> class.
    /// </summary>
    /// <param name="threshold">
    /// Minimum ratio of current ATR to ATR Floor required to pass filter.
    /// <para>Range: 0.5-1.2 (typically 0.90-1.0)</para>
    /// <para>- 1.0 (100%): Current ATR must equal or exceed recent average</para>
    /// <para>- 0.95 (95%): Recommended - small tolerance for natural fluctuation</para>
    /// <para>- 0.90 (90%): More permissive, only blocks significantly dead markets</para>
    /// <para>- 0.80 (80%): Very lenient, rarely blocks</para>
    /// </param>
    /// <param name="atrPeriod">
    /// Period for ATR (volatility) calculation.
    /// <para>Standard: 14 (Wilder's original)</para>
    /// <para>Must be >= 1. Typical range: 7-30.</para>
    /// </param>
    /// <param name="historyLookback">
    /// Number of recent candles to include when calculating ATR baseline.
    /// <para>Must be >= atrPeriod + 1</para>
    /// <para>- 30: Fast adaptation to regime changes, less stable</para>
    /// <para>- 50: Recommended - good balance of stability and adaptation</para>
    /// <para>- 100+: Very stable baseline, slow to adapt</para>
    /// </param>
    /// <param name="atrEmaPeriod">
    /// EMA smoothing period applied to ATR history to create ATR Floor.
    /// <para>Must be >= 1. Typical range: 15-30.</para>
    /// <para>- 15: Faster adaptation to volatility changes</para>
    /// <para>- 20: Recommended - balanced smoothing</para>
    /// <para>- 30: Very smooth, only major changes affect it</para>
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when parameters are invalid:
    /// - atrPeriod &lt; 1
    /// - historyLookback &lt; atrPeriod + 1
    /// - atrEmaPeriod &lt; 1
    /// - threshold &lt;= 0
    /// </exception>
    public VolatilityFilter(
        decimal threshold = 0.95m,
        int atrPeriod = 14,
        int historyLookback = 50,
        int atrEmaPeriod = 20)
        : base(
            $"VolFilter({threshold},ATR:{atrPeriod},EMA:{atrEmaPeriod},LB:{historyLookback})",
            "Blocks signals when current ATR is below a smoothed ATR floor (EMA of recent ATR values)")
    {
        if (atrPeriod < 1)
            throw new ArgumentException("atrPeriod must be >= 1", nameof(atrPeriod));
        if (historyLookback < atrPeriod + 1)
            throw new ArgumentException("historyLookback must be >= atrPeriod + 1", nameof(historyLookback));
        if (atrEmaPeriod < 1)
            throw new ArgumentException("atrEmaPeriod must be >= 1", nameof(atrEmaPeriod));
        if (threshold <= 0)
            throw new ArgumentException("threshold must be > 0", nameof(threshold));

        _threshold = threshold;
        _atrPeriod = atrPeriod;
        _historyLookback = historyLookback;

        _atrIndicator = new AtrIndicator(id: "VolatilityFilter_ATR", period: _atrPeriod);
        _emaIndicatorForAtr = new EmaIndicator(id: "VolatilityFilter_ATR_EMA", period: atrEmaPeriod);
    }

    /// <summary>
    /// Evaluates whether current volatility (ATR) exceeds the dynamic threshold.
    /// </summary>
    /// <param name="context">
    /// Market context containing candle history for ATR calculation.
    /// Requires at least (atrPeriod + 1) candles.
    /// </param>
    /// <returns>
    /// <see cref="FilterResult"/> indicating:
    /// <list type="bullet">
    ///   <item><description>Passed: true if CurrentATR > ATRFloor × Threshold (sufficient volatility)</description></item>
    ///   <item><description>Passed: false if volatility too low or insufficient data</description></item>
    ///   <item><description>Reason: Description of why filter passed/failed</description></item>
    ///   <item><description>Diagnostics: CurrentATR, ATRFloor, Threshold, and other debug info</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// Algorithm steps:
    /// </para>
    /// <list type="number">
    ///   <item><description>Compute ATR over all candles in context</description></item>
    ///   <item><description>Extract recent ATR values (last historyLookback candles, skipping nulls)</description></item>
    ///   <item><description>Compute EMA over extracted ATR values to get ATR Floor (baseline)</description></item>
    ///   <item><description>Compare current ATR to ATR Floor × Threshold</description></item>
    /// </list>
    /// <para>
    /// Performance: O(N) where N is number of candles. ATR and EMA both computed in single pass.
    /// Typical execution time: &lt;1ms for 1000 candles.
    /// </para>
    /// <para>
    /// Error handling:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Insufficient candles: Fails with InsufficientData reason</description></item>
    ///   <item><description>ATR computation failed: Fails with ATRComputeFailed reason</description></item>
    ///   <item><description>Current ATR null/NaN: Fails with CurrentATRUnavailable reason</description></item>
    ///   <item><description>Not enough ATR history: Fails with InsufficientATRHistory reason</description></item>
    ///   <item><description>ATR Floor computation failed: Fails with ATREMAComputeFailed reason</description></item>
    ///   <item><description>ATR Floor null/NaN: Fails with ATRFloorUnavailable reason</description></item>
    /// </list>
    /// </remarks>
    protected override FilterResult EvaluateCore(IMarketContext context)
    {
        var candles = context.Candles;

        // Check minimum data requirement
        if (candles.Count < _atrPeriod + 1)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = $"InsufficientData: need {_atrPeriod + 1} candles, have {candles.Count}",
                EvaluatedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["RequiredCandles"] = _atrPeriod + 1,
                    ["ActualCandles"] = candles.Count
                }
            };
        }

        // Step 1: Calculate ATR series over all candles
        var atrResult = _atrIndicator.Compute(candles);

        // Check if ATR computation was successful
        if (!atrResult.IsValid)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = $"ATRComputeFailed: {atrResult.ErrorReason}",
                EvaluatedAt = context.TimestampUtc,
                Diagnostics = atrResult.Diagnostics
            };
        }

        // Get the ATR values list (IReadOnlyList<IndicatorValue>)
        var atrValues = atrResult.Values;

        if (atrValues.Count == 0)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = "ATRComputeFailed: no values returned",
                EvaluatedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object> { ["Period"] = _atrPeriod }
            };
        }

        // Get current (most recent) ATR value
        var currentAtrValue = atrValues[^1];

        if (!currentAtrValue.Value.HasValue || double.IsNaN(currentAtrValue.Value.Value))
        {
            return new FilterResult
            {
                Passed = false,
                Reason = "CurrentATRUnavailable: still in warm-up period or invalid",
                EvaluatedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["Period"] = _atrPeriod,
                    ["ValuesCount"] = atrValues.Count
                }
            };
        }

        var currentAtr = currentAtrValue.Value.Value;

        // Step 2: Collect up to historyLookback recent ATR values (skip nulls)
        var recentAtrValues = new List<double>(_historyLookback);
        int startIndex = Math.Max(0, atrValues.Count - _historyLookback);

        for (int i = startIndex; i < atrValues.Count; i++)
        {
            var val = atrValues[i];
            if (val.Value.HasValue && !double.IsNaN(val.Value.Value))
            {
                recentAtrValues.Add(val.Value.Value);
            }
        }

        // Need at least 2 values to compute meaningful EMA
        if (recentAtrValues.Count < 2)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = $"InsufficientATRHistory: have {recentAtrValues.Count}, need at least 2",
                EvaluatedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["Have"] = recentAtrValues.Count,
                    ["NeedAtLeast"] = 2
                }
            };
        }

        // Step 3: Compute EMA over ATR values to get ATR Floor (baseline)
        // We create synthetic "candles" where Close = ATR value
        // This allows us to reuse EmaIndicator to smooth the ATR series
        var atrAsCandles = recentAtrValues.Select(v => new Candle
        {
            Open = (decimal)v,
            High = (decimal)v,
            Low = (decimal)v,
            Close = (decimal)v,
            Volume = 0,
            Time = DateTime.UtcNow // Timestamp doesn't matter for this calculation
        }).ToList();

        var atrEmaResult = _emaIndicatorForAtr.Compute(atrAsCandles);

        // Check if EMA computation was successful
        if (!atrEmaResult.IsValid)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = $"ATREMAComputeFailed: {atrEmaResult.ErrorReason}",
                EvaluatedAt = context.TimestampUtc,
                Diagnostics = atrEmaResult.Diagnostics
            };
        }

        var atrEmaValues = atrEmaResult.Values;

        if (atrEmaValues.Count == 0)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = "ATREMAComputeFailed: no values returned",
                EvaluatedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["ATRHistoryCount"] = recentAtrValues.Count
                }
            };
        }

        // Get ATR Floor (the smoothed baseline)
        var atrFloorValue = atrEmaValues[^1];

        if (!atrFloorValue.Value.HasValue || double.IsNaN(atrFloorValue.Value.Value))
        {
            return new FilterResult
            {
                Passed = false,
                Reason = "ATRFloorUnavailable: EMA returned null or NaN",
                EvaluatedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["ATRHistoryCount"] = recentAtrValues.Count
                }
            };
        }

        var atrFloor = atrFloorValue.Value.Value;

        // Step 4: Make the decision
        // Pass if: CurrentATR > ATRFloor × Threshold
        var requiredAtr = atrFloor * (double)_threshold;
        bool passed = currentAtr > requiredAtr;

        // Calculate how much above/below threshold we are (for diagnostics)
        var atrRatio = currentAtr / atrFloor;
        var percentOfThreshold = (currentAtr / requiredAtr) * 100.0;

        return new FilterResult
        {
            Passed = passed,
            Reason = passed
                ? $"Ok: ATR {currentAtr:F5} > Floor {atrFloor:F5} × {_threshold} ({atrRatio:F2}x average)"
                : $"LowVolatility: ATR {currentAtr:F5} ≤ Floor {atrFloor:F5} × {_threshold} ({atrRatio:F2}x average, {percentOfThreshold:F1}% of required)",
            EvaluatedAt = context.TimestampUtc,
            Diagnostics = new Dictionary<string, object>
            {
                ["CurrentATR"] = currentAtr,
                ["ATRFloor"] = atrFloor,
                ["RequiredATR"] = requiredAtr,
                ["Threshold"] = _threshold,
                ["ATRRatio"] = atrRatio,
                ["PercentOfThreshold"] = percentOfThreshold,
                ["ATRHistoryCount"] = recentAtrValues.Count,
                ["ATRTimestamp"] = currentAtrValue.Timestamp
            }
        };
    }
}