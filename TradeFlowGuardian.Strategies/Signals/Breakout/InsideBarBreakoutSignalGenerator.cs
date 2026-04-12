using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Signals.Base;

namespace TradeFlowGuardian.Strategies.Signals;

/// <summary>
/// Generates signals when price breaks out of an inside bar (compression) pattern.
/// </summary>
/// <remarks>
/// <para>
/// The InsideBarBreakoutSignal identifies volatility compression patterns where a "child" candle's
/// entire range (high and low) is contained within the range of a preceding "mother" candle, then
/// signals when price breaks out of this consolidation. This pattern represents a pause in market
/// action followed by a directional commitment, often occurring at key decision points like
/// support/resistance tests or before trend continuation/reversal.
/// </para>
/// <para>
/// Pattern Recognition Logic:
/// </para>
/// <list type="number">
///   <item><description>Identifies a mother bar (reference candle) from lookback period</description></item>
///   <item><description>Checks if the most recent candle (child) is fully contained: child.High ≤ mother.High AND child.Low ≥ mother.Low</description></item>
///   <item><description>If confirmation is required, waits for the NEXT bar to break mother's range</description></item>
///   <item><description>If confirmation is disabled, signals immediately when child bar closes beyond mother's range</description></item>
///   <item><description>Validates pattern quality using compression ratio (mother range / child range)</description></item>
///   <item><description>Generates Long signal when breakout occurs above mother's high</description></item>
///   <item><description>Generates Short signal when breakout occurs below mother's low</description></item>
/// </list>
/// <para>
/// Inside Bar Trading Psychology:
/// </para>
/// <para>
/// Inside bars represent equilibrium where neither bulls nor bears have control. They often form when:
/// </para>
/// <list type="bullet">
///   <item><description>Price tests a key level and participants wait for confirmation</description></item>
///   <item><description>Market consolidates mid-trend before continuation</description></item>
///   <item><description>Volatility contracts before major news or market open</description></item>
///   <item><description>Traders take profits, creating temporary balance</description></item>
/// </list>
/// <para>
/// The subsequent breakout represents the resolution of this equilibrium, with the direction indicating
/// which side (bulls or bears) gained control. The tighter the compression (higher compression ratio),
/// the more explosive the breakout typically is.
/// </para>
/// <para>
/// Confidence Calculation:
/// </para>
/// <para>
/// Signal confidence combines pattern quality and breakout strength:
/// </para>
/// <list type="bullet">
///   <item><description>Base confidence: 0.5 (50%) for any valid inside bar pattern</description></item>
///   <item><description>Compression bonus: +0.3 maximum based on ratio of mother range to child range (peaks at 3:1 ratio)</description></item>
///   <item><description>Breakout strength bonus: +0.2 maximum based on how far price travels beyond mother's range</description></item>
///   <item><description>Formula: Confidence = Min(1.0, 0.5 + compressionComponent + breakoutComponent)</description></item>
///   <item><description>compressionComponent: 0.3 × Min(1.0, compressionRatio / 3.0)</description></item>
///   <item><description>breakoutComponent: 0.2 × Min(1.0, normalizedDistance × 5.0)</description></item>
/// </list>
/// <para>
/// This approach rewards tight compressions (more energy buildup) and decisive breakouts
/// (stronger directional conviction).
/// </para>
/// <para>
/// Confirmation Bar Strategy:
/// </para>
/// <para>
/// By default, the signal requires confirmation (next bar breaks mother's range) which reduces
/// false signals but may result in slightly worse entry prices. When <c>requireConfirmationBar = false</c>,
/// the signal fires immediately when the inside bar closes beyond the mother's range, providing
/// earlier entries but with higher risk of whipsaw.
/// </para>
/// <para>
/// Usage Examples:
/// </para>
/// <code>
/// // Example 1: Conservative inside bar with confirmation (default)
/// var conservative = new InsideBarBreakoutSignal(
///     id: "insidebar_confirmed",
///     lookback: 1,                    // Check immediate previous bar as mother
///     requireConfirmationBar: true,   // Wait for next bar to confirm
///     minCompressionRatio: 2.0m       // Mother must be 2x larger than child
/// );
/// 
/// // Example 2: Aggressive inside bar without confirmation
/// var aggressive = new InsideBarBreakoutSignal(
///     id: "insidebar_immediate",
///     lookback: 1,
///     requireConfirmationBar: false,  // Signal on inside bar close
///     minCompressionRatio: 1.5m       // Less strict compression requirement
/// );
/// 
/// // Example 3: Multi-bar inside pattern
/// var multiBar = new InsideBarBreakoutSignal(
///     id: "insidebar_2bar",
///     lookback: 2,                    // Check 2 bars back for mother
///     requireConfirmationBar: true,
///     minCompressionRatio: 2.5m       // Stricter requirement for older mother
/// );
/// 
/// // Example 4: Using the signal in a strategy
/// var result = conservative.Generate(context);
/// if (result.Direction == SignalDirection.Long &amp;&amp; result.Confidence > 0.75)
/// {
///     Console.WriteLine($"High-quality inside bar breakout!");
///     Console.WriteLine($"Compression ratio: {result.Diagnostics["CompressionRatio"]:F2}");
///     Console.WriteLine($"Mother bar range: {result.Diagnostics["MotherRange"]:F5}");
/// }
/// </code>
/// <para>
/// Parameter Tuning Guidelines:
/// </para>
/// <list type="bullet">
///   <item><description>lookback: 1 for standard inside bar, 2-3 for multi-bar patterns (less common but higher quality)</description></item>
///   <item><description>requireConfirmationBar: true for higher win rate (conservative), false for better R:R (aggressive)</description></item>
///   <item><description>minCompressionRatio: 1.5-2.0 for active trading, 2.5-3.0 for higher quality setups</description></item>
///   <item><description>Timeframes: 4H and Daily are optimal; 1H can work; avoid &lt;1H (too noisy)</description></item>
/// </list>
/// <para>
/// Optimal Market Conditions:
/// </para>
/// <list type="bullet">
///   <item><description>Best: Inside bars forming at key support/resistance levels in trending markets</description></item>
///   <item><description>Good: Inside bars in the direction of the prevailing trend (continuation patterns)</description></item>
///   <item><description>Avoid: Multiple consecutive inside bars (indecision), extremely low volatility periods</description></item>
/// </list>
/// <para>
/// Limitations and Considerations:
/// </para>
/// <list type="bullet">
///   <item><description>Inside bars on very small timeframes (1m, 5m) are often market noise rather than meaningful patterns</description></item>
///   <item><description>Extremely small mother bars (dojis) produce low-quality signals regardless of compression ratio</description></item>
///   <item><description>Pattern works best when combined with trend context (e.g., TrendFilter) or key level identification</description></item>
///   <item><description>Consecutive inside bars (nested patterns) should be treated differently - may indicate deeper consolidation</description></item>
///   <item><description>False breakouts can occur in choppy markets; consider combining with volume confirmation</description></item>
/// </list>
/// <para>
/// Complementary Signals:
/// </para>
/// <para>
/// Inside bar signals are particularly powerful when combined with:
/// </para>
/// <list type="bullet">
///   <item><description>BreakoutSignal: Inside bar forming near range boundary = high-probability breakout</description></item>
///   <item><description>TrendFilter: Inside bars in the direction of the trend have higher success rates</description></item>
///   <item><description>Support/Resistance: Inside bars at key levels represent decision points</description></item>
/// </list>
/// </remarks>
public sealed class InsideBarBreakoutSignal : SignalBase
{
    private readonly int _lookback;
    private readonly bool _requireConfirmationBar;
    private readonly decimal _minCompressionRatio;

    /// <summary>
    /// Initializes a new instance of the <see cref="InsideBarBreakoutSignal"/> class with specified parameters.
    /// </summary>
    /// <param name="id">
    /// Unique identifier for this signal instance. Used for diagnostics and signal composition.
    /// Convention: lowercase with underscores (e.g., "insidebar_1", "insidebar_confirmed").
    /// </param>
    /// <param name="lookback">
    /// Number of bars to look back for the mother bar. 
    /// <list type="bullet">
    ///   <item><description>1: Standard inside bar (most common) - checks immediate previous bar</description></item>
    ///   <item><description>2-3: Multi-bar patterns - checks older bars for larger mother bars</description></item>
    ///   <item><description>&gt;3: Rarely useful, may miss pattern significance</description></item>
    /// </list>
    /// Default: 1 (standard inside bar).
    /// Valid range: 1-5 periods.
    /// </param>
    /// <param name="requireConfirmationBar">
    /// Whether to wait for the next bar to confirm the breakout.
    /// <list type="bullet">
    ///   <item><description>true (default): Conservative approach, waits for next bar's close beyond mother's range. 
    ///   Reduces false signals, slightly worse entry price</description></item>
    ///   <item><description>false: Aggressive approach, signals when inside bar closes beyond mother's range.
    ///   Earlier entries, higher risk of whipsaw</description></item>
    /// </list>
    /// Recommendation: Use true for higher win rate strategies, false for scalping or when combined with additional filters.
    /// </param>
    /// <param name="minCompressionRatio">
    /// Minimum ratio of mother bar range to child bar range for a valid pattern.
    /// Higher values filter for tighter compressions (higher quality patterns).
    /// <list type="bullet">
    ///   <item><description>1.0: Any inside bar qualifies (very loose, noisy)</description></item>
    ///   <item><description>1.5-2.0: Good balance for active trading</description></item>
    ///   <item><description>2.5-3.0: High-quality setups, fewer signals</description></item>
    ///   <item><description>&gt;3.0: Rare, extremely tight compressions only</description></item>
    /// </list>
    /// Default: 1.5 (reasonable quality threshold).
    /// Valid range: 1.0-5.0.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if id is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown if lookback &lt; 1, lookback &gt; 5, or minCompressionRatio &lt; 1.0.
    /// </exception>
    /// <example>
    /// <code>
    /// // Standard inside bar with default settings
    /// var signal = new InsideBarBreakoutSignal(
    ///     id: "insidebar_standard",
    ///     lookback: 1,
    ///     requireConfirmationBar: true,
    ///     minCompressionRatio: 1.5m
    /// );
    /// </code>
    /// </example>
    public InsideBarBreakoutSignal(
        string id,
        int lookback = 1,
        bool requireConfirmationBar = true,
        decimal minCompressionRatio = 1.5m)
        : base(id, $"InsideBarBreakout(lb:{lookback},conf:{requireConfirmationBar},ratio:{minCompressionRatio:F1})")
    {
        if (lookback < 1 || lookback > 5)
            throw new ArgumentException("Lookback must be between 1 and 5", nameof(lookback));
        if (minCompressionRatio < 1.0m)
            throw new ArgumentException("Minimum compression ratio must be >= 1.0", nameof(minCompressionRatio));

        _lookback = lookback;
        _requireConfirmationBar = requireConfirmationBar;
        _minCompressionRatio = minCompressionRatio;
    }

    /// <summary>
    /// Core signal generation logic that evaluates market context for inside bar breakout patterns.
    /// </summary>
    /// <param name="context">
    /// Immutable market context containing price history, indicators, and timestamp.
    /// Must contain at least (_lookback + 2) candles if confirmation required, or (_lookback + 1) if not.
    /// </param>
    /// <returns>
    /// <see cref="SignalResult"/> with:
    /// <list type="bullet">
    ///   <item><description>Long signal: When price breaks above mother bar's high with confidence based on pattern quality and breakout strength</description></item>
    ///   <item><description>Short signal: When price breaks below mother bar's low with confidence based on pattern quality and breakout strength</description></item>
    ///   <item><description>Neutral signal: When no inside bar pattern exists, pattern quality is insufficient, or no breakout has occurred</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// Signal Triggering Logic:
    /// </para>
    /// <para>
    /// With confirmation (default):
    /// </para>
    /// <list type="number">
    ///   <item><description>Bar N-lookback = mother bar</description></item>
    ///   <item><description>Bar N-1 = child bar (inside bar), must be fully within mother's range</description></item>
    ///   <item><description>Bar N (current) = confirmation bar, must close beyond mother's range</description></item>
    /// </list>
    /// <para>
    /// Without confirmation:
    /// </para>
    /// <list type="number">
    ///   <item><description>Bar N-lookback = mother bar</description></item>
    ///   <item><description>Bar N (current) = child bar, must be inside mother AND close beyond mother's range</description></item>
    /// </list>
    /// <para>
    /// Pattern Quality Validation:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Mother bar must have meaningful range (&gt; 0)</description></item>
    ///   <item><description>Child bar must be fully contained within mother's range</description></item>
    ///   <item><description>Compression ratio must meet minimum threshold</description></item>
    ///   <item><description>Child bar must have some range (&gt; 0) to avoid division by zero</description></item>
    /// </list>
    /// <para>
    /// Early Exit Conditions:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Insufficient data: Returns neutral if not enough candles for pattern detection</description></item>
    ///   <item><description>No inside bar: Returns neutral if child is not fully contained within mother</description></item>
    ///   <item><description>Low quality pattern: Returns neutral if compression ratio below threshold</description></item>
    ///   <item><description>No breakout: Returns neutral if price hasn't broken mother's range</description></item>
    /// </list>
    /// <para>
    /// Diagnostic Information:
    /// </para>
    /// <para>
    /// All signal results include comprehensive diagnostics for analysis:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>MotherHigh/MotherLow: Boundary values of the mother bar</description></item>
    ///   <item><description>MotherRange: Total range of mother bar (High - Low)</description></item>
    ///   <item><description>ChildHigh/ChildLow: Boundary values of the inside (child) bar</description></item>
    ///   <item><description>ChildRange: Total range of child bar</description></item>
    ///   <item><description>CompressionRatio: Mother range divided by child range (quality metric)</description></item>
    ///   <item><description>BreakoutClose: The close price that triggered the breakout</description></item>
    ///   <item><description>BreakoutDistance: How far beyond mother's boundary the breakout traveled</description></item>
    ///   <item><description>RequiredConfirmation: Whether confirmation bar was required</description></item>
    ///   <item><description>Lookback: Configuration parameter for traceability</description></item>
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
        // Determine minimum required candles based on confirmation setting
        var minCandles = _requireConfirmationBar ? _lookback + 2 : _lookback + 1;

        if (context.Candles.Count < minCandles)
            return NeutralResult(
                $"Insufficient data: need {minCandles}, have {context.Candles.Count}",
                context.TimestampUtc);

        // Identify the mother bar (reference bar for the inside pattern)
        var motherIndex = _requireConfirmationBar
            ? context.Candles.Count - _lookback - 2 // Mother is 2 bars back when confirming
            : context.Candles.Count - _lookback - 1; // Mother is 1 bar back when not confirming

        var mother = context.Candles[motherIndex];
        var motherRange = mother.High - mother.Low;

        // Validate mother bar has meaningful range
        if (motherRange <= 0)
            return NeutralResult(
                $"Mother bar has no range (doji)",
                context.TimestampUtc);

        // Identify the child bar (potential inside bar)
        var childIndex = _requireConfirmationBar
            ? context.Candles.Count - 2 // Child is previous bar when confirming
            : context.Candles.Count - 1; // Child is current bar when not confirming

        var child = context.Candles[childIndex];
        var childRange = child.High - child.Low;

        // Check if child bar is truly inside the mother bar
        bool isInsideBar = child.High <= mother.High && child.Low >= mother.Low;

        if (!isInsideBar)
            return NeutralResult(
                "No inside bar pattern detected",
                context.TimestampUtc);

        // Validate pattern quality: check compression ratio
        // Avoid division by zero for extremely small child bars (near-doji)
        if (childRange <= 0)
            return NeutralResult(
                "Child bar has no range (doji inside bar)",
                context.TimestampUtc);

        var compressionRatio = (double)(motherRange / childRange);

        if (compressionRatio < (double)_minCompressionRatio)
            return NeutralResult(
                $"Compression ratio {compressionRatio:F2} below minimum {_minCompressionRatio:F2}",
                context.TimestampUtc);

        // Identify the breakout bar (bar that must break mother's range)
        var breakoutBar = _requireConfirmationBar
            ? context.Candles[^1] // Current bar confirms the breakout
            : child; // Child bar itself breaks out (no confirmation)

        // Check for bullish breakout (close above mother's high)
        if (breakoutBar.Close > mother.High)
        {
            var breakoutDistance = breakoutBar.Close - mother.High;
            var normalizedDistance = (double)(breakoutDistance / motherRange);

            // Calculate confidence: base + compression quality + breakout strength
            // Base: 0.5 for valid inside bar
            // Compression: 0.3 max (scaled by ratio, peaks at 3:1)
            // Breakout: 0.2 max (scaled by distance relative to mother range)
            var compressionComponent = 0.3 * Math.Min(1.0, compressionRatio / 3.0);
            var breakoutComponent = 0.2 * Math.Min(1.0, normalizedDistance * 5.0);
            var confidence = Math.Min(1.0, 0.5 + compressionComponent + breakoutComponent);

            return new SignalResult
            {
                Direction = SignalDirection.Long,
                Confidence = confidence,
                Reason =
                    $"Bullish inside bar breakout: Close={breakoutBar.Close:F5} > Mother.High={mother.High:F5}, Compression={compressionRatio:F2}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["MotherHigh"] = mother.High,
                    ["MotherLow"] = mother.Low,
                    ["MotherRange"] = motherRange,
                    ["ChildHigh"] = child.High,
                    ["ChildLow"] = child.Low,
                    ["ChildRange"] = childRange,
                    ["CompressionRatio"] = compressionRatio,
                    ["BreakoutClose"] = breakoutBar.Close,
                    ["BreakoutDistance"] = breakoutDistance,
                    ["NormalizedDistance"] = normalizedDistance,
                    ["RequiredConfirmation"] = _requireConfirmationBar,
                    ["Lookback"] = _lookback
                }
            };
        }

        // Check for bearish breakout (close below mother's low)
        if (breakoutBar.Close < mother.Low)
        {
            var breakoutDistance = mother.Low - breakoutBar.Close;
            var normalizedDistance = (double)(breakoutDistance / motherRange);

            // Calculate confidence using same formula as bullish breakout
            var compressionComponent = 0.3 * Math.Min(1.0, compressionRatio / 3.0);
            var breakoutComponent = 0.2 * Math.Min(1.0, normalizedDistance * 5.0);
            var confidence = Math.Min(1.0, 0.5 + compressionComponent + breakoutComponent);

            return new SignalResult
            {
                Direction = SignalDirection.Short,
                Confidence = confidence,
                Reason =
                    $"Bearish inside bar breakout: Close={breakoutBar.Close:F5} < Mother.Low={mother.Low:F5}, Compression={compressionRatio:F2}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["MotherHigh"] = mother.High,
                    ["MotherLow"] = mother.Low,
                    ["MotherRange"] = motherRange,
                    ["ChildHigh"] = child.High,
                    ["ChildLow"] = child.Low,
                    ["ChildRange"] = childRange,
                    ["CompressionRatio"] = compressionRatio,
                    ["BreakoutClose"] = breakoutBar.Close,
                    ["BreakoutDistance"] = breakoutDistance,
                    ["NormalizedDistance"] = normalizedDistance,
                    ["RequiredConfirmation"] = _requireConfirmationBar,
                    ["Lookback"] = _lookback
                }
            };
        }

        // Inside bar pattern detected but no breakout yet
        return NeutralResult(
            $"Inside bar detected (compression={compressionRatio:F2}) but no breakout: Close={breakoutBar.Close:F5} within [{mother.Low:F5}, {mother.High:F5}]",
            context.TimestampUtc);
    }
}