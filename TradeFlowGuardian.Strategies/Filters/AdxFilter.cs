using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Filters.Base;

namespace TradeFlowGuardian.Strategies.Filters;

/// <summary>
/// Filter that passes when ADX indicates sufficient trend strength for the trend-following strategies.
/// </summary>
/// <remarks>
/// <para>
/// 📚 What Does This Filter Do?
/// </para>
/// <para>
/// The ADX Filter acts as a "trend strength gatekeeper" - it only allows trades when the market has enough
/// directional movement to justify trend-following strategies. This is one of the most important filters
/// because it prevents you from using trend strategies in choppy, ranging markets where they fail.
/// </para>
/// <para>
/// Think of ADX as asking: "Is there enough of a trend to make a trend-following trade worthwhile?"
/// If ADX is too low, the market is just bouncing around - your SMA crossover or breakout strategy will
/// get whipsawed. This filter says "no thanks, let's wait for a real trend."
/// </para>
/// <para>
/// 🎯 Common Usage Patterns:
/// </para>
/// <list type="bullet">
///   <item><description>Trend Strategy Filter: Only take SMA/EMA crossover signals when ADX > 25 (trending market)</description></item>
///   <item><description>Range Strategy Filter (inverse): Only take RSI reversion signals when ADX &lt; 20 (ranging market)</description></item>
///   <item><description>Volatility Confirmation: Combine with ATR filter to ensure both trend and volatility are present</description></item>
///   <item><description>Dynamic Strategy Selection: Use ADX to switch between trend-following and mean-reversion strategies</description></item>
/// </list>
/// <para>
/// 💡 Key Insight: ADX measures trend STRENGTH, not direction. A rising ADX means the trend (up OR down) is
/// getting stronger. You still need to check price/SMA/other indicators to know which direction to trade.
/// </para>
/// <para>
/// ⚙️ Parameters Explained:
/// </para>
/// <list type="bullet">
///   <item><description>Threshold: ADX level required to pass filter
///     <para>- 20: Minimum for considering a trend exists (conservative)</para>
///     <para>- 25: Standard threshold used by most traders (recommended starting point)</para>
///     <para>- 30: Strong trend requirement (fewer but higher quality signals)</para>
///     <para>- 40+: Very strong trend (rare, but high probability)</para>
///   </description></item>
///   <item><description>Period: ADX calculation period (default: 14)
///     <para>- Shorter (7-10): More responsive, catches trend changes faster</para>
///     <para>- Standard (14): Wilder's original, balanced approach</para>
///     <para>- Longer (20-30): Smoother, only signals very established trends</para>
///   </description></item>
///   <item><description>Inverse: Flip the logic (pass when ADX is BELOW threshold)
///     <para>- false (default): Pass when ADX >= threshold (for trend strategies)</para>
///     <para>- true: Pass when ADX &lt; threshold (for range-bound strategies like RSI reversion)</para>
///   </description></item>
/// </list>
/// <para>
/// 🎬 Real-World Examples:
/// </para>
/// <example>
/// <code>
/// // Example 1: Basic trend filter for SMA crossover strategy
/// // Only take crossover signals when there's a real trend
/// var trendFilter = new AdxFilter(
///     id: "adx_trend_25",
///     threshold: 25m,
///     period: 14,
///     inverse: false  // Pass when ADX >= 25
/// );
/// 
/// // This prevents SMA crossover signals in choppy markets
/// // where ADX &lt; 25 indicates no clear trend
/// 
/// // Example 2: Range-bound filter for mean-reversion strategy
/// // Only take RSI oversold/overbought signals in ranging markets
/// var rangeFilter = new AdxFilter(
///     id: "adx_range_20",
///     threshold: 20m,
///     period: 14,
///     inverse: true  // Pass when ADX &lt; 20
/// );
/// 
/// // This filters out RSI signals during strong trends
/// // where "oversold" doesn't mean reversal, it means continuation
/// 
/// // Example 3: Aggressive trend filter for breakout strategy
/// // Only trade breakouts when trend is very strong
/// var strongTrendFilter = new AdxFilter(
///     id: "adx_strong_30",
///     threshold: 30m,
///     period: 14,
///     inverse: false
/// );
/// 
/// // Fewer signals, but higher win rate on breakouts
/// 
/// // Example 4: Using in a composite filter
/// // Combine ADX with other filters for robust entry conditions
/// var entryFilter = new AndFilter(
///     "entry_conditions",
///     new AdxFilter("adx_25", 25m),           // Trending market
///     new RsiThresholdFilter("rsi_not_overbought", 70m, inverse: true),  // Not overbought
///     new TimeFilter("london_session", 8, 16)  // During London hours
/// );
/// 
/// // Only enter when:
/// // 1. ADX shows trend strength
/// // 2. RSI shows we're not chasing at extreme
/// // 3. It's during active market hours
/// </code>
/// </example>
/// <para>
/// ⚠️ Common Mistakes and Solutions:
/// </para>
/// <list type="bullet">
///   <item><description>❌ Mistake: Using same ADX threshold for all markets
///     <para>Problem: Volatile pairs (GBP/JPY) naturally have higher ADX. EUR/USD might need ADX>25, GBP/JPY might need >35.</para>
///     <para>✅ Solution: Backtest to find optimal threshold for each pair. More volatile = higher threshold needed.</para>
///   </description></item>
///   <item><description>❌ Mistake: Not using inverse mode for range strategies
///     <para>Problem: Trading RSI mean-reversion during strong trends (ADX>40) = fighting the trend = losses.</para>
///     <para>✅ Solution: For range strategies, use inverse=true to only trade when ADX&lt;20 (no strong trend).</para>
///   </description></item>
///   <item><description>❌ Mistake: Using very low threshold (like ADX>15)
///     <para>Problem: Too permissive - you're essentially not filtering anything. ADX>15 happens 80% of the time.</para>
///     <para>✅ Solution: Use minimum of 20, preferably 25. You want to be selective, not just tick a box.</para>
///   </description></item>
///   <item><description>❌ Mistake: Forgetting ADX doesn't show direction
///     <para>Problem: "ADX is 40, so I should buy!" - NO! ADX could be 40 in a downtrend.</para>
///     <para>✅ Solution: Always combine with directional indicator (price vs SMA, +DI/-DI, trend filter).</para>
///   </description></item>
/// </list>
/// <para>
/// 🎓 Tuning Guidelines by Strategy Type:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Strategy Type</term>
///     <description>ADX Filter Settings</description>
///     <description>Why?</description>
///   </listheader>
///   <item>
///     <term>Breakout Trading</term>
///     <description>threshold: 25-30, inverse: false</description>
///     <description>Need trend strength to confirm breakout is real, not false</description>
///   </item>
///   <item>
///     <term>Trend Following (SMA/EMA)</term>
///     <description>threshold: 20-25, inverse: false</description>
///     <description>Standard trend confirmation, catches most trending periods</description>
///   </item>
///   <item>
///     <term>Mean Reversion (RSI)</term>
///     <description>threshold: 15-20, inverse: true</description>
///     <description>Only trade when NO strong trend present, ranges bounce</description>
///   </item>
///   <item>
///     <term>Momentum Trading</term>
///     <description>threshold: 30-40, inverse: false</description>
///     <description>Need very strong trend for momentum continuation plays</description>
///   </item>
///   <item>
///     <term>Support/Resistance</term>
///     <description>threshold: 20-25, inverse: true</description>
///     <description>S/R works best in ranges, fails in strong trends</description>
///   </item>
/// </list>
/// <para>
/// 🏆 Best Practices:
/// </para>
/// <list type="number">
///   <item><description>Start with ADX>25: This is the industry standard for "trending market." Adjust from there based on backtesting.</description></item>
///   <item><description>Use inverse for range strategies: If your strategy profits from mean reversion, you MUST filter out trending markets.</description></item>
///   <item><description>Don't use ADX alone: Combine with directional filter (SMA, trend line) to know which way to trade.</description></item>
///   <item><description>Monitor false rejection rate: If your filter blocks 90% of signals, threshold might be too high. Aim for 50-70% rejection.</description></item>
///   <item><description>Different thresholds for different timeframes: Lower timeframes need higher ADX (more noise). Daily might use 20, M15 might need 30.</description></item>
/// </list>
/// <para>
/// 📊 Impact on Trading Results:
/// </para>
/// <para>
/// Adding an ADX filter typically:
/// </para>
/// <list type="bullet">
///   <item><description>Reduces number of trades by 40-60% (blocks choppy periods)</description></item>
///   <item><description>Increases win rate by 10-20% (better quality setups)</description></item>
///   <item><description>Reduces maximum drawdown by 20-30% (avoids whipsaw periods)</description></item>
///   <item><description>May reduce total profit slightly (fewer trades) but improves risk-adjusted returns (Sharpe ratio)</description></item>
/// </list>
/// <para>
/// The trade-off: Fewer signals but much higher quality. You're saying "I'll only trade when conditions are favorable."
/// </para>
/// </remarks>
public class AdxFilter : FilterBase
{
    private readonly int _period;
    private readonly decimal _threshold;
    private readonly bool _inverse;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdxFilter"/> class.
    /// </summary>
    /// <param name="id">
    /// Unique identifier for this filter instance (e.g., "adx_trend_25", "adx_range_20").
    /// Used for diagnostics and debugging filter chains.
    /// </param>
    /// <param name="threshold">
    /// ADX value that must be met (or not met, if inverse=true) for filter to pass.
    /// <para>Common values:</para>
    /// <list type="bullet">
    ///   <item><description>20: Minimum trend strength (conservative, more signals)</description></item>
    ///   <item><description>25: Standard threshold (recommended starting point)</description></item>
    ///   <item><description>30: Strong trend requirement (selective, fewer signals)</description></item>
    ///   <item><description>40+: Very strong trend (rare but high quality)</description></item>
    /// </list>
    /// <para>Must be positive. Typical range: 15-40.</para>
    /// </param>
    /// <param name="period">
    /// ADX calculation period. Must match the period of the ADX indicator in your market context.
    /// <para>Standard: 14 (Wilder's original recommendation)</para>
    /// <para>If you're using a custom ADX period, set this to match it.</para>
    /// </param>
    /// <param name="inverse">
    /// Whether to invert the filter logic.
    /// <para>- false (default): Pass when ADX >= threshold (for trend strategies)</para>
    /// <para>- true: Pass when ADX &lt; threshold (for range-bound strategies)</para>
    /// <para>Use inverse=true when trading mean-reversion strategies that profit from ranges.</para>
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period &lt; 2 or threshold &lt;= 0.
    /// </exception>
    public AdxFilter(
        string id,
        decimal threshold,
        int period = 14,
        bool inverse = false) : base(id, $"ADX({threshold},p={period},inv={inverse})")
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 2");
        if (threshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be > 0");

        _period = period;
        _threshold = threshold;
        _inverse = inverse;
    }

    /// <summary>
    /// Evaluates whether the current ADX value meets the threshold criteria.
    /// </summary>
    /// <param name="context">
    /// Market context containing the ADX indicator results.
    /// The context must have an "ADX" indicator with computed values.
    /// </param>
    /// <returns>
    /// <see cref="FilterResult"/> indicating:
    /// <list type="bullet">
    ///   <item><description>Passed: true if ADX meets criteria (>= threshold, or &lt; threshold if inverse)</description></item>
    ///   <item><description>Reason: Human-readable explanation of why filter passed/failed</description></item>
    ///   <item><description>Diagnostics: Contains ADX value, threshold, period, and inverse flag</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// This filter looks for an indicator named "ADX" in the market context. If you're using a custom
    /// indicator ID, you'll need to modify this filter or ensure your pipeline registers the ADX
    /// indicator with the ID "ADX".
    /// </para>
    /// <para>
    /// The filter uses the most recent (last) ADX value from the indicator's Values collection.
    /// This corresponds to the current candle being evaluated.
    /// </para>
    /// <para>
    /// Error handling:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>If ADX indicator not found: Filter fails with descriptive reason</description></item>
    ///   <item><description>If ADX indicator invalid: Filter fails with error reason from indicator</description></item>
    ///   <item><description>If ADX value is null: Filter fails (insufficient data for ADX calculation)</description></item>
    /// </list>
    /// <para>
    /// Performance: O(1) - just retrieves last value from pre-computed indicator results.
    /// </para>
    /// </remarks>
    protected override FilterResult EvaluateCore(IMarketContext context)
    {
        // Try to get the ADX indicator result from the context
        // The indicator should have been computed by the pipeline before filters run
        if (!context.Indicators.TryGetValue("ADX", out var adxResult))
        {
            return new FilterResult
            {
                Passed = false,
                Reason =
                    "ADX indicator not found in market context. Ensure pipeline computes ADX indicator with ID 'ADX'.",
                EvaluatedAt = context.TimestampUtc
            };
        }

        // Check if the indicator computation was successful
        if (!adxResult.IsValid)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = $"ADX indicator computation failed: {adxResult.ErrorReason}",
                EvaluatedAt = context.TimestampUtc
            };
        }

        // Get the ADX values list (not Value property - that doesn't exist!)
        // IIndicatorResult.Values returns IReadOnlyList<IndicatorValue>
        var adxValues = adxResult.Values;

        // Check if we have any values
        if (adxValues.Count == 0)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = "ADX indicator has no values (insufficient data)",
                EvaluatedAt = context.TimestampUtc
            };
        }

        // Get the most recent ADX value (last in the list)
        var currentAdxValue = adxValues[^1];

        // Check if the value is null (can happen during warm-up period)
        if (!currentAdxValue.Value.HasValue)
        {
            return new FilterResult
            {
                Passed = false,
                Reason = "Current ADX value is null (indicator still in warm-up period)",
                EvaluatedAt = context.TimestampUtc
            };
        }

        var currentAdx = (decimal)currentAdxValue.Value.Value;

        // Evaluate the threshold criteria
        // Normal mode: Pass if ADX >= threshold (trending market)
        // Inverse mode: Pass if ADX < threshold (ranging market)
        bool passed = _inverse
            ? currentAdx < _threshold
            : currentAdx >= _threshold;

        // Build descriptive reason
        string comparison = _inverse ? "<" : ">=";
        string marketState = _inverse
            ? (passed ? "ranging market (good for mean-reversion)" : "trending market (avoid mean-reversion)")
            : (passed ? "trending market (good for trend-following)" : "ranging market (avoid trend-following)");

        return new FilterResult
        {
            Passed = passed,
            Reason = $"ADX={currentAdx:F2} {comparison} {_threshold} → {marketState}",
            EvaluatedAt = context.TimestampUtc,
            Diagnostics = new Dictionary<string, object>
            {
                ["CurrentADX"] = currentAdx,
                ["Threshold"] = _threshold,
                ["Period"] = _period,
                ["Inverse"] = _inverse,
                ["Comparison"] = comparison,
                ["ADXTimestamp"] = currentAdxValue.Timestamp
            }
        };
    }
}