using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Signals.Base;

namespace TradeFlowGuardian.Strategies.Signals;

/// <summary>
/// Generates signals when a fast Simple Moving Average (SMA) crosses a slow SMA.
/// </summary>
/// <remarks>
/// <para>
/// The SMACrossoverSignal is a classic trend-following indicator that identifies potential
/// trend changes or continuations by detecting when a shorter-period SMA crosses above or below
/// a longer-period SMA. SMA crossovers use equal weighting for all periods in the calculation window,
/// making them more stable and less reactive to recent price noise compared to EMA crossovers.
/// This characteristic makes SMA crossovers better suited for longer timeframes and more conservative
/// trading approaches.
/// </para>
/// <para>
/// Strategy Logic:
/// </para>
/// <list type="number">
///   <item><description>Computes fast SMA (shorter period, e.g., 20) on closing prices</description></item>
///   <item><description>Computes slow SMA (longer period, e.g., 50) on closing prices</description></item>
///   <item><description>Detects crossover by comparing current and previous bar relationships</description></item>
///   <item><description>Generates Long signal when fast SMA crosses above slow SMA (Golden Cross)</description></item>
///   <item><description>Generates Short signal when fast SMA crosses below slow SMA (Death Cross)</description></item>
///   <item><description>Returns Neutral when SMAs are aligned but no crossover occurs</description></item>
/// </list>
/// <para>
/// SMA Characteristics:
/// </para>
/// <list type="bullet">
///   <item><description>Equal weighting: All prices in the window have equal influence (1/N weight each)</description></item>
///   <item><description>Calculation: Sum of N closing prices divided by N</description></item>
///   <item><description>Smoothness: Very smooth, filters out short-term noise effectively</description></item>
///   <item><description>Lag: Higher lag than EMA, slower to react to price changes</description></item>
///   <item><description>Stability: Less prone to false signals, but may miss quick reversals</description></item>
/// </list>
/// <para>
/// SMA vs EMA - Key Differences:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Characteristic</term>
///     <description>SMA</description>
///     <description>EMA</description>
///   </listheader>
///   <item>
///     <term>Weighting</term>
///     <description>Equal weight (1/N)</description>
///     <description>Exponential decay (recent = heavier)</description>
///   </item>
///   <item>
///     <term>Responsiveness</term>
///     <description>Slow, filters noise well</description>
///     <description>Fast, reacts to recent moves</description>
///   </item>
///   <item>
///     <term>Lag</term>
///     <description>Higher lag</description>
///     <description>Lower lag</description>
///   </item>
///   <item>
///     <term>False Signals</term>
///     <description>Fewer false signals</description>
///     <description>More false signals in chop</description>
///   </item>
///   <item>
///     <term>Best Timeframes</term>
///     <description>Daily, Weekly (position trading)</description>
///     <description>1H, 4H (day/swing trading)</description>
///   </item>
///   <item>
///     <term>Market Type</term>
///     <description>Trending markets, established trends</description>
///     <description>Dynamic markets, emerging trends</description>
///   </item>
/// </list>
/// <para>
/// Confidence Calculation:
/// </para>
/// <para>
/// Signal confidence is based on the separation between the two SMAs at the crossover point:
/// </para>
/// <list type="bullet">
///   <item><description>Base confidence: 0.5 (50%) for any valid crossover detection</description></item>
///   <item><description>Separation bonus: Up to +0.5 based on percentage separation between SMAs</description></item>
///   <item><description>Formula: Confidence = Min(1.0, 0.5 + (|FastSMA - SlowSMA| / SlowSMA) × 50)</description></item>
///   <item><description>Rationale: Wider separation = stronger momentum = higher conviction</description></item>
/// </list>
/// <para>
/// Since SMA crossovers occur with less frequency than EMA crossovers (due to higher lag),
/// the separation at the crossover point is often more meaningful, representing more established momentum.
/// </para>
/// <para>
/// Famous SMA Crossovers:
/// </para>
/// <list type="bullet">
///   <item><description>Golden Cross: SMA(50) crosses above SMA(200) = Major bullish signal</description></item>
///   <item><description>Death Cross: SMA(50) crosses below SMA(200) = Major bearish signal</description></item>
///   <item><description>9/21 Cross: SMA(9) × SMA(21) = Short-term trend changes</description></item>
///   <item><description>20/50 Cross: SMA(20) × SMA(50) = Medium-term trend changes</description></item>
/// </list>
/// <para>
/// These crosses are widely monitored by institutions and retail traders, often creating self-fulfilling
/// prophecies. However, by the time major crosses like Golden/Death Cross occur, significant trend
/// movement has typically already occurred.
/// </para>
/// <para>
/// Common Period Combinations:
/// </para>
/// <list type="bullet">
///   <item><description>Short-term (intraday): SMA(10, 30) or SMA(15, 45) - faster signals, noisier</description></item>
///   <item><description>Medium-term (swing): SMA(20, 50) or SMA(9, 21) - balanced approach</description></item>
///   <item><description>Long-term (position): SMA(50, 200) or SMA(100, 200) - major trend identification</description></item>
///   <item><description>Conservative approach: Use ratios of 3:1 to 5:1 (e.g., 20:60, 50:200) for clearer separation</description></item>
/// </list>
/// <para>
/// Usage Examples:
/// </para>
/// <code>
/// // Example 1: Medium-term swing trading setup
/// var swingTrading = new SMACrossoverSignal(
///     id: "sma_cross_20_50",
///     fastPeriods: 20,
///     slowPeriods: 50
/// );
/// 
/// // Example 2: Famous Golden Cross / Death Cross
/// var goldenCross = new SMACrossoverSignal(
///     id: "sma_golden_cross",
///     fastPeriods: 50,
///     slowPeriods: 200
/// );
/// 
/// // Example 3: Short-term crossover
/// var shortTerm = new SMACrossoverSignal(
///     id: "sma_cross_9_21",
///     fastPeriods: 9,
///     slowPeriods: 21
/// );
/// 
/// // Example 4: Using the signal with confidence threshold
/// var result = swingTrading.Generate(context);
/// if (result.Direction == SignalDirection.Long &amp;&amp; result.Confidence > 0.70)
/// {
///     Console.WriteLine($"Strong bullish trend detected!");
///     Console.WriteLine($"SMA separation: {result.Diagnostics["SeparationPercent"]:P2}");
///     Console.WriteLine($"This is a {(result.Confidence > 0.85 ? "high" : "moderate")} quality signal");
/// }
/// 
/// // Example 5: Golden Cross with high conviction
/// var goldenResult = goldenCross.Generate(context);
/// if (goldenResult.Direction == SignalDirection.Long)
/// {
///     Console.WriteLine("🌟 GOLDEN CROSS DETECTED! 🌟");
///     Console.WriteLine("Major bullish market structure shift");
///     Console.WriteLine($"Consider long-term bullish positioning");
/// }
/// </code>
/// <para>
/// Parameter Tuning Guidelines:
/// </para>
/// <list type="bullet">
///   <item><description>Period ratio: Slow/Fast ratio of 2.5-5.0 recommended for clear separation (e.g., 20/50, 50/200)</description></item>
///   <item><description>Shorter periods (5-20): More signals but noisier; only use on Daily+ timeframes</description></item>
///   <item><description>Medium periods (20-50): Balanced quality/frequency; good for swing trading</description></item>
///   <item><description>Longer periods (50-200): Rare but high-quality signals; position trading only</description></item>
///   <item><description>Timeframe rule: Minimum period × 2 = minimum candle timeframe in minutes (e.g., SMA(50) → 4H+ timeframe)</description></item>
/// </list>
/// <para>
/// Optimal Market Conditions:
/// </para>
/// <list type="bullet">
///   <item><description>Best: Established trending markets with consistent directional momentum</description></item>
///   <item><description>Good: Post-consolidation breakouts where trend is forming</description></item>
///   <item><description>Acceptable: Choppy markets with longer SMA periods (200+) to filter noise</description></item>
///   <item><description>Avoid: Tight ranging markets, sideways consolidation, very low volatility periods</description></item>
///   <item><description>Never use: On timeframes &lt; 1H (SMA lag too high for intraday noise)</description></item>
/// </list>
/// <para>
/// Advantages Over EMA:
/// </para>
/// <list type="bullet">
///   <item><description>Stability: Less whipsaw in choppy markets due to equal weighting</description></item>
///   <item><description>Simplicity: Easier to calculate and understand conceptually</description></item>
///   <item><description>Historical significance: More widely recognized by institutions (Golden/Death Cross)</description></item>
///   <item><description>False signal reduction: Higher lag filters out noise, reducing losing trades</description></item>
///   <item><description>Better backtests: Often performs better on longer timeframes (Daily+)</description></item>
/// </list>
/// <para>
/// Disadvantages vs EMA:
/// </para>
/// <list type="bullet">
///   <item><description>Late entries: Signals occur well after trend starts, missing optimal entry</description></item>
///   <item><description>Late exits: Slow to recognize reversals, giving back more profits</description></item>
///   <item><description>Frequency: Fewer signals overall, less trading opportunities</description></item>
///   <item><description>Responsiveness: Can't adapt quickly to rapid market changes</description></item>
///   <item><description>Intraday unsuitability: Too slow for daytrading and scalping</description></item>
/// </list>
/// <para>
/// Limitations and Considerations:
/// </para>
/// <list type="bullet">
///   <item><description>Extreme lag: SMA is the most lagging of all MA types; crossovers often miss 20-40% of trend</description></item>
///   <item><description>Ranging market failures: Generates frequent false signals in sideways markets</description></item>
///   <item><description>No magnitude indication: Doesn't tell you how strong the trend will be or duration</description></item>
///   <item><description>Equal weight problem: Today's price has same weight as N days ago, ignoring recent momentum</description></item>
///   <item><description>Institutional front-running: Famous crosses (50/200) often front-run by smart money</description></item>
/// </list>
/// <para>
/// Complementary Signals and Filters:
/// </para>
/// <para>
/// SMA crossover signals are significantly more effective when combined with:
/// </para>
/// <list type="bullet">
///   <item><description>Volume confirmation: Crossover on high volume = stronger signal validity</description></item>
///   <item><description>Higher TF trend: Only take crossovers aligned with higher timeframe SMA direction</description></item>
///   <item><description>Support/Resistance: Crossovers at key levels have higher follow-through probability</description></item>
///   <item><description>ADX filter: Only trade when ADX &gt; 25 (strong trend present)</description></item>
///   <item><description>RSI divergence: Crossover + RSI divergence = high-probability reversal</description></item>
///   <item><description>Price action: Wait for higher high/lower low after crossover for confirmation</description></item>
/// </list>
/// <para>
/// When to Choose SMA Over EMA:
/// </para>
/// <list type="bullet">
///   <item><description>Trading Daily or Weekly charts (position trading)</description></item>
///   <item><description>Want fewer, higher-quality signals (lower frequency strategy)</description></item>
///   <item><description>Markets with lots of intraday noise but clear longer-term trends</description></item>
///   <item><description>Conservative risk profile (avoiding whipsaws more important than early entry)</description></item>
///   <item><description>Building systematic strategies that need stability over responsiveness</description></item>
///   <item><description>Want to follow "famous" institutional signals (Golden/Death Cross)</description></item>
/// </list>
/// <para>
/// When to Choose EMA Over SMA:
/// </para>
/// <list type="bullet">
///   <item><description>Trading 1H to 4H charts (day/swing trading)</description></item>
///   <item><description>Want faster entries closer to optimal price</description></item>
///   <item><description>Volatile markets where quick response is valuable</description></item>
///   <item><description>Aggressive risk profile (willing to take more signals for better entries)</description></item>
///   <item><description>Need to capture shorter-term momentum moves</description></item>
/// </list>
/// <para>
/// Historical Performance Notes:
/// </para>
/// <para>
/// Academic studies show SMA crossover systems typically:
/// </para>
/// <list type="bullet">
///   <item><description>Win rate: 35-45% (minority of trades win)</description></item>
///   <item><description>Profit factor: 1.2-1.6 (profitable overall despite low win rate)</description></item>
///   <item><description>Works due to: Large winners compensating for many small losers (trend-following nature)</description></item>
///   <item><description>Performs best: In strongly trending asset classes (commodities, indices)</description></item>
///   <item><description>Performs worst: In mean-reverting assets (some FX pairs, range-bound stocks)</description></item>
/// </list>
/// </remarks>
public sealed class SmaCrossoverSignal : SignalBase
{
    private readonly int _fastPeriods;
    private readonly int _slowPeriods;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmaCrossoverSignal"/> class with specified period parameters.
    /// </summary>
    /// <param name="id">
    /// Unique identifier for this signal instance. Used for diagnostics and signal composition.
    /// Convention: lowercase with underscores (e.g., "sma_cross_20_50", "sma_golden_cross").
    /// </param>
    /// <param name="fastPeriods">
    /// Number of periods for the fast (shorter) SMA. Should be significantly less than slow periods.
    /// <list type="bullet">
    ///   <item><description>9-20: Short-term, responsive but noisy</description></item>
    ///   <item><description>20-50: Medium-term, balanced quality</description></item>
    ///   <item><description>50-100: Long-term, very stable</description></item>
    /// </list>
    /// Default: 20 periods.
    /// Valid range: 5-200 periods.
    /// </param>
    /// <param name="slowPeriods">
    /// Number of periods for the slow (longer) SMA. Should be 2.5-5× larger than fast periods for clear separation.
    /// <list type="bullet">
    ///   <item><description>21-50: Medium-term trends</description></item>
    ///   <item><description>50-100: Long-term trends</description></item>
    ///   <item><description>100-200: Major market structure (institutional focus)</description></item>
    /// </list>
    /// Default: 50 periods.
    /// Valid range: 10-300 periods.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if id is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown if fastPeriods &lt; 5, slowPeriods &lt;= fastPeriods, slowPeriods &gt; 300, or ratio &lt; 1.5.
    /// </exception>
    /// <example>
    /// <code>
    /// // Standard medium-term swing trading
    /// var signal = new SMACrossoverSignal(
    ///     id: "sma_20_50",
    ///     fastPeriods: 20,
    ///     slowPeriods: 50
    /// );
    /// 
    /// // Famous Golden Cross setup
    /// var goldenCross = new SMACrossoverSignal(
    ///     id: "golden_cross",
    ///     fastPeriods: 50,
    ///     slowPeriods: 200
    /// );
    /// </code>
    /// </example>
    public SmaCrossoverSignal(string id, int fastPeriods = 20, int slowPeriods = 50)
        : base(id, $"SMACross({fastPeriods},{slowPeriods})")
    {
        if (fastPeriods < 5)
            throw new ArgumentException("Fast periods must be >= 5", nameof(fastPeriods));
        if (slowPeriods <= fastPeriods)
            throw new ArgumentException("Slow periods must be greater than fast periods", nameof(slowPeriods));
        if (slowPeriods > 300)
            throw new ArgumentException("Slow periods must be <= 300", nameof(slowPeriods));

        // Validate reasonable ratio for meaningful separation
        var ratio = (double)slowPeriods / fastPeriods;
        if (ratio < 1.5)
            throw new ArgumentException($"Slow/Fast ratio ({ratio:F2}) should be >= 1.5 for meaningful separation",
                nameof(slowPeriods));

        _fastPeriods = fastPeriods;
        _slowPeriods = slowPeriods;
    }

    /// <summary>
    /// Computes Simple Moving Average (SMA) for a series of closing prices.
    /// </summary>
    /// <param name="prices">
    /// List of closing prices. Must contain at least the period count for valid SMA calculation.
    /// </param>
    /// <param name="period">
    /// Number of periods for SMA calculation. Each price has equal weight (1/period).
    /// </param>
    /// <returns>
    /// The SMA value for the most recent price in the series. Returns 0 if insufficient data.
    /// </returns>
    /// <remarks>
    /// <para>
    /// SMA Calculation Formula:
    /// </para>
    /// <para>
    /// SMA = (Sum of last N closing prices) / N
    /// </para>
    /// <para>
    /// Where N is the period. Each price contributes equally (1/N weight) to the average.
    /// This equal weighting is both SMA's strength (stability, noise filtering) and weakness
    /// (lag, slow response to recent price action).
    /// </para>
    /// <para>
    /// Example: SMA(5) on prices [100, 102, 101, 103, 105]
    /// </para>
    /// <para>
    /// SMA = (100 + 102 + 101 + 103 + 105) / 5 = 511 / 5 = 102.2
    /// </para>
    /// <para>
    /// Computational Efficiency:
    /// </para>
    /// <para>
    /// This implementation uses LINQ's Sum() and Average() for clarity. For performance-critical
    /// applications with large datasets, consider implementing a rolling window approach that
    /// maintains a running sum, adding new prices and subtracting old ones (O(1) per update vs O(N)).
    /// </para>
    /// <para>
    /// Note: This is an inline computation for simplicity. For production systems with multiple
    /// SMA-based signals, consider using a shared SMA indicator instance from the indicator pipeline
    /// to avoid redundant calculations and improve performance, especially in backtesting scenarios.
    /// </para>
    /// </remarks>
    private static decimal ComputeSma(IReadOnlyList<decimal> prices, int period)
    {
        return prices.Count < period ? 0m :
            // Take the last 'period' prices and calculate their average
            prices.Skip(prices.Count - period).Take(period).Average();
    }

    /// <summary>
    /// Core signal generation logic that evaluates market context for SMA crossover conditions.
    /// </summary>
    /// <param name="context">
    /// Immutable market context containing price history, indicators, and timestamp.
    /// Must contain at least (slowPeriods + 2) candles for reliable crossover detection.
    /// </param>
    /// <returns>
    /// <see cref="SignalResult"/> with:
    /// <list type="bullet">
    ///   <item><description>Long signal: When fast SMA crosses above slow SMA (Golden Cross pattern) with confidence based on separation</description></item>
    ///   <item><description>Short signal: When fast SMA crosses below slow SMA (Death Cross pattern) with confidence based on separation</description></item>
    ///   <item><description>Neutral signal: When no crossover detected or insufficient data</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// Data Requirements:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Need slowPeriods + 1 for current SMA calculation</description></item>
    ///   <item><description>Need +1 additional for previous bar comparison (total: slowPeriods + 2)</description></item>
    ///   <item><description>More data improves SMA stability (warmup period highly recommended)</description></item>
    ///   <item><description>Ideally have 2× slowPeriods for full SMA convergence</description></item>
    /// </list>
    /// <para>
    /// Crossover Detection Process:
    /// </para>
    /// <list type="number">
    ///   <item><description>Extract closing prices from candles</description></item>
    ///   <item><description>Compute current bar SMAs (both fast and slow)</description></item>
    ///   <item><description>Compute previous bar SMAs (using closes[0..^1])</description></item>
    ///   <item><description>Compare relationships to detect crossover</description></item>
    ///   <item><description>Calculate confidence based on SMA separation percentage</description></item>
    /// </list>
    /// <para>
    /// Early Exit Conditions:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Insufficient data: Returns neutral if candle count &lt; (slowPeriods + 2)</description></item>
    ///   <item><description>No crossover: Returns neutral if SMAs maintain same relationship on both bars</description></item>
    /// </list>
    /// <para>
    /// Diagnostic Information:
    /// </para>
    /// <para>
    /// All signal results include comprehensive diagnostics for analysis:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>FastCurrent/FastPrevious: Fast SMA values on current and previous bars</description></item>
    ///   <item><description>SlowCurrent/SlowPrevious: Slow SMA values on current and previous bars</description></item>
    ///   <item><description>Separation: Absolute difference between fast and slow SMAs</description></item>
    ///   <item><description>SeparationPercent: Separation as percentage of slow SMA (normalized measure)</description></item>
    ///   <item><description>FastPeriods/SlowPeriods: Configuration parameters for traceability</description></item>
    ///   <item><description>IsGoldenCross/IsDeathCross: Boolean flags for famous 50/200 cross (if applicable)</description></item>
    /// </list>
    /// <para>
    /// Determinism:
    /// </para>
    /// <para>
    /// This method is deterministic: given identical market context, it will always produce
    /// identical results. It does not access system time or random sources. Uses context.TimestampUtc
    /// for result timestamp to ensure backtest reproducibility.
    /// </para>
    /// </remarks>
    protected override SignalResult GenerateCore(IMarketContext context)
    {
        // Validate sufficient data: need slow period + 2 (one for current, one for previous comparison)
        if (context.Candles.Count < _slowPeriods + 2)
            return NeutralResult(
                $"Insufficient data: need {_slowPeriods + 2}, have {context.Candles.Count}",
                context.TimestampUtc);

        // Extract closing prices for SMA calculation
        var closes = context.Candles.Select(c => c.Close).ToList();
        var prevCloses = closes.Take(closes.Count - 1).ToList();

        // Compute current bar SMAs
        var fastNow = ComputeSma(closes, _fastPeriods);
        var slowNow = ComputeSma(closes, _slowPeriods);

        // Compute previous bar SMAs for crossover detection
        var fastPrev = ComputeSma(prevCloses, _fastPeriods);
        var slowPrev = ComputeSma(prevCloses, _slowPeriods);

        // Detect bullish crossover (Golden Cross): fast was below or equal, now above
        bool bullishCross = fastPrev <= slowPrev && fastNow > slowNow;

        // Detect bearish crossover (Death Cross): fast was above or equal, now below
        bool bearishCross = fastPrev >= slowPrev && fastNow < slowNow;

        // Check if this is the famous Golden Cross or Death Cross (50/200)
        bool isGoldenDeathCross = (_fastPeriods == 50 && _slowPeriods == 200) ||
                                  (_fastPeriods == 200 && _slowPeriods == 50);

        if (bullishCross)
        {
            // Calculate separation and confidence
            var separation = fastNow - slowNow;
            var separationPercent = (double)(separation / slowNow);

            // Confidence: base 0.5 + bonus up to 0.5 based on percentage separation
            // 1% separation = 100% confidence, scales linearly
            var confidence = Math.Min(1.0, 0.5 + Math.Abs(separationPercent) * 50.0);

            var crossName = isGoldenDeathCross && bullishCross ? "GOLDEN CROSS" : "Bullish SMA crossover";

            return new SignalResult
            {
                Direction = SignalDirection.Long,
                Confidence = confidence,
                Reason =
                    $"{crossName}: Fast SMA({_fastPeriods})={fastNow:F5} crossed above Slow SMA({_slowPeriods})={slowNow:F5}, Separation={separationPercent:P2}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["FastCurrent"] = fastNow,
                    ["FastPrevious"] = fastPrev,
                    ["SlowCurrent"] = slowNow,
                    ["SlowPrevious"] = slowPrev,
                    ["Separation"] = separation,
                    ["SeparationPercent"] = separationPercent,
                    ["FastPeriods"] = _fastPeriods,
                    ["SlowPeriods"] = _slowPeriods,
                    ["IsGoldenCross"] = isGoldenDeathCross && bullishCross,
                    ["CrossType"] = "Bullish"
                }
            };
        }

        if (bearishCross)
        {
            // Calculate separation and confidence
            var separation = slowNow - fastNow;
            var separationPercent = (double)(separation / slowNow);

            // Confidence calculation same as bullish
            var confidence = Math.Min(1.0, 0.5 + Math.Abs(separationPercent) * 50.0);

            var crossName = isGoldenDeathCross && bearishCross ? "DEATH CROSS" : "Bearish SMA crossover";

            return new SignalResult
            {
                Direction = SignalDirection.Short,
                Confidence = confidence,
                Reason =
                    $"{crossName}: Fast SMA({_fastPeriods})={fastNow:F5} crossed below Slow SMA({_slowPeriods})={slowNow:F5}, Separation={separationPercent:P2}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["FastCurrent"] = fastNow,
                    ["FastPrevious"] = fastPrev,
                    ["SlowCurrent"] = slowNow,
                    ["SlowPrevious"] = slowPrev,
                    ["Separation"] = separation,
                    ["SeparationPercent"] = separationPercent,
                    ["FastPeriods"] = _fastPeriods,
                    ["SlowPeriods"] = _slowPeriods,
                    ["IsDeathCross"] = isGoldenDeathCross && bearishCross,
                    ["CrossType"] = "Bearish"
                }
            };
        }

        // No crossover detected: SMAs aligned but no crossing event
        return NeutralResult(
            $"No crossover: Fast={fastNow:F5}, Slow={slowNow:F5}, FastPrev={fastPrev:F5}, SlowPrev={slowPrev:F5}",
            context.TimestampUtc);
    }
}