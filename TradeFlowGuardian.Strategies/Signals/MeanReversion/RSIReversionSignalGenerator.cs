using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Signals.Base;

namespace TradeFlowGuardian.Strategies.Signals.MeanReversion;

/// <summary>
/// Generates mean-reversion signals based on Relative Strength Index (RSI) overbought and oversold conditions.
/// </summary>
/// <remarks>
/// <para>
/// The RSIReversionSignal identifies potential reversal opportunities when price has moved too far too fast,
/// as measured by the RSI oscillator entering extreme zones. Unlike trend-following signals (crossovers, breakouts),
/// this is a counter-trend strategy that assumes prices will revert to their mean after reaching extremes.
/// RSI mean reversion is particularly effective in ranging markets and for catching short-term pullbacks
/// within larger trends.
/// </para>
/// <para>
/// RSI Fundamentals:
/// </para>
/// <list type="bullet">
///   <item><description>Range: RSI oscillates between 0 and 100</description></item>
///   <item><description>Neutral zone: 30-70 (no signal)</description></item>
///   <item><description>Oversold zone: Below 30 (potential bullish reversal)</description></item>
///   <item><description>Overbought zone: Above 70 (potential bearish reversal)</description></item>
///   <item><description>Extreme oversold: Below 20 (strong reversal potential)</description></item>
///   <item><description>Extreme overbought: Above 80 (strong reversal potential)</description></item>
/// </list>
/// <para>
/// Strategy Logic:
/// </para>
/// <list type="number">
///   <item><description>Computes RSI over specified period (typically 14 bars)</description></item>
///   <item><description>Checks if RSI has entered oversold zone (below oversold threshold)</description></item>
///   <item><description>Generates Long signal when RSI is oversold (expects price to bounce)</description></item>
///   <item><description>Checks if RSI has entered overbought zone (above overbought threshold)</description></item>
///   <item><description>Generates Short signal when RSI is overbought (expects price to fall)</description></item>
///   <item><description>Returns Neutral when RSI is within normal range (30-70)</description></item>
/// </list>
/// <para>
/// RSI Calculation (Wilder's Method):
/// </para>
/// <list type="number">
///   <item><description>Calculate price changes: Gains (positive changes) and Losses (negative changes, absolute value)</description></item>
///   <item><description>Calculate initial average gain and average loss over N periods (typically 14)</description></item>
///   <item><description>For subsequent periods, use smoothing: AvgGain = (PrevAvgGain × 13 + CurrentGain) / 14</description></item>
///   <item><description>Calculate Relative Strength (RS) = AvgGain / AvgLoss</description></item>
///   <item><description>Calculate RSI = 100 - (100 / (1 + RS))</description></item>
/// </list>
/// <para>
/// Confidence Calculation:
/// </para>
/// <para>
/// Signal confidence is based on how deep into the extreme zone RSI has penetrated:
/// </para>
/// <list type="bullet">
///   <item><description>Base confidence: 0.5 (50%) when RSI just enters extreme zone</description></item>
///   <item><description>Depth bonus: Up to +0.5 based on distance into extreme zone</description></item>
///   <item><description>Oversold formula: Confidence = Min(1.0, 0.5 + (Oversold - RSI) / Oversold × 0.5)</description></item>
///   <item><description>Overbought formula: Confidence = Min(1.0, 0.5 + (RSI - Overbought) / (100 - Overbought) × 0.5)</description></item>
/// </list>
/// <para>
/// Examples with default thresholds (30/70):
/// </para>
/// <list type="bullet">
///   <item><description>RSI = 30 → ~50% confidence (just entered oversold)</description></item>
///   <item><description>RSI = 20 → ~67% confidence (moderately oversold)</description></item>
///   <item><description>RSI = 10 → ~83% confidence (extremely oversold)</description></item>
///   <item><description>RSI = 0 → 100% confidence (maximum oversold, very rare)</description></item>
/// </list>
/// <para>
/// Common RSI Period and Threshold Combinations:
/// </para>
/// <list type="bullet">
///   <item><description>Standard (14, 30, 70): Wilder's original parameters, most widely used</description></item>
///   <item><description>Sensitive (9, 30, 70): Faster signals, more whipsaws, day trading</description></item>
///   <item><description>Smooth (21, 30, 70): Slower signals, fewer false signals, swing trading</description></item>
///   <item><description>Conservative (14, 20, 80): Only extreme reversals, higher quality but less frequent</description></item>
///   <item><description>Aggressive (14, 40, 60): More signals, earlier entries but in trending markets prone to failure</description></item>
/// </list>
/// <para>
/// Usage Examples:
/// </para>
/// <code>
/// // Example 1: Standard RSI mean reversion
/// var standard = new RSIReversionSignal(
///     id: "rsi_reversion_standard",
///     period: 14,
///     oversold: 30m,
///     overbought: 70m
/// );
/// 
/// // Example 2: Conservative approach (only extreme reversals)
/// var conservative = new RSIReversionSignal(
///     id: "rsi_reversion_conservative",
///     period: 14,
///     oversold: 20m,
///     overbought: 80m
/// );
/// 
/// // Example 3: Aggressive scalping
/// var aggressive = new RSIReversionSignal(
///     id: "rsi_reversion_scalp",
///     period: 9,
///     oversold: 35m,
///     overbought: 65m
/// );
/// 
/// // Example 4: Using the signal with confidence analysis
/// var result = standard.Generate(context);
/// if (result.Direction == SignalDirection.Long &amp;&amp; result.Confidence > 0.65)
/// {
///     var rsi = (decimal)result.Diagnostics["RSI"];
///     Console.WriteLine($"Strong oversold reversal signal!");
///     Console.WriteLine($"RSI: {rsi:F2} (deeply oversold)");
///     Console.WriteLine($"Confidence: {result.Confidence:P0}");
///     
///     if (rsi < 20)
///         Console.WriteLine("⚠️ Extremely oversold - high reversal probability");
/// }
/// 
/// // Example 5: Combining with trend context
/// // Only take oversold signals in uptrend (with-trend pullbacks)
/// if (result.Direction == SignalDirection.Long)
/// {
///     // Check if price is above 200 SMA (uptrend context)
///     // This significantly improves win rate
/// }
/// </code>
/// <para>
/// Parameter Tuning Guidelines:
/// </para>
/// <list type="bullet">
///   <item><description>Period: 9 for fast/scalping, 14 for standard, 21-25 for smooth/position trading</description></item>
///   <item><description>Thresholds in trending markets: 20/80 (wait for extreme reversals)</description></item>
///   <item><description>Thresholds in ranging markets: 30/70 or even 40/60 (more opportunities)</description></item>
///   <item><description>Volatile pairs (GBP/JPY): Use wider thresholds (20/80) or longer period (21)</description></item>
///   <item><description>Stable pairs (EUR/USD): Standard thresholds (30/70) work well</description></item>
/// </list>
/// <para>
/// Optimal Market Conditions:
/// </para>
/// <list type="bullet">
///   <item><description>Best: Ranging/sideways markets with clear support and resistance</description></item>
///   <item><description>Good: Pullbacks within established trends (use with trend filter)</description></item>
///   <item><description>Acceptable: Consolidation phases after strong moves</description></item>
///   <item><description>Poor: Strong trending markets (RSI stays overbought/oversold for extended periods)</description></item>
///   <item><description>Avoid: Breakout scenarios, news events, very low volatility</description></item>
/// </list>
/// <para>
/// Mean Reversion vs Trend Following:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Characteristic</term>
///     <description>Mean Reversion (RSI)</description>
///     <description>Trend Following (MA Cross)</description>
///   </listheader>
///   <item>
///     <term>Market type</term>
///     <description>Ranging, sideways</description>
///     <description>Trending, directional</description>
///   </item>
///   <item>
///     <term>Win rate</term>
///     <description>Higher (60-70%)</description>
///     <description>Lower (35-45%)</description>
///   </item>
///   <item>
///     <term>Avg win size</term>
///     <description>Smaller</description>
///     <description>Larger</description>
///   </item>
///   <item>
///     <term>Hold time</term>
///     <description>Short (hours to days)</description>
///     <description>Long (days to weeks)</description>
///   </item>
///   <item>
///     <term>Risk profile</term>
///     <description>Catching knives (risky)</description>
///     <description>Riding trends (safer)</description>
///   </item>
/// </list>
/// <para>
/// The "Catching a Falling Knife" Problem:
/// </para>
/// <para>
/// RSI mean reversion signals counter-trend trades, which is inherently risky. In strong trends,
/// RSI can remain overbought/oversold for extended periods while price continues moving. This is why:
/// </para>
/// <list type="bullet">
///   <item><description>RSI mean reversion should NEVER be the sole decision factor</description></item>
///   <item><description>Must be combined with support/resistance levels or trend context</description></item>
///   <item><description>Stop losses are critical (price can keep falling when RSI is oversold)</description></item>
///   <item><description>Consider waiting for RSI to start turning back before entry (e.g., RSI rises above 25 after hitting 20)</description></item>
/// </list>
/// <para>
/// RSI Divergences (Not Implemented Here):
/// </para>
/// <para>
/// While this signal focuses on absolute RSI levels, RSI divergences are even more powerful:
/// </para>
/// <list type="bullet">
///   <item><description>Bullish divergence: Price makes lower low, RSI makes higher low = strong reversal signal</description></item>
///   <item><description>Bearish divergence: Price makes higher high, RSI makes lower high = strong reversal signal</description></item>
///   <item><description>Implementation note: Divergence detection requires swing point identification and price comparison, typically implemented as a separate signal</description></item>
/// </list>
/// <para>
/// Advantages of RSI Mean Reversion:
/// </para>
/// <list type="bullet">
///   <item><description>High win rate: Prices do tend to revert to mean in ranging markets</description></item>
///   <item><description>Defined entry/exit: Clear overbought/oversold zones provide entry, and neutral zone (50) provides exit</description></item>
///   <item><description>Quick trades: Mean reversion happens faster than trend following</description></item>
///   <item><description>Multiple opportunities: Ranges produce many signals vs few trend signals</description></item>
///   <item><description>Risk/reward clarity: Entry at extreme, target at mean provides good R:R</description></item>
/// </list>
/// <para>
/// Limitations and Dangers:
/// </para>
/// <list type="bullet">
///   <item><description>Trend destruction: In strong trends, RSI stays extreme and mean reversion fails catastrophically</description></item>
///   <item><description>Small winners: Mean reversion produces many small wins vs trend following's large winners</description></item>
///   <item><description>False signals: RSI can touch extreme and continue further (e.g., 25 → 15 → 8)</description></item>
///   <item><description>Whipsaw in transition: When market shifts from ranging to trending, signals fail frequently</description></item>
///   <item><description>No trend context: Signal doesn't know if you're buying into a downtrend or selling into an uptrend</description></item>
///   <item><description>Lag: RSI is a lagging indicator, reversal may have already started</description></item>
/// </list>
/// <para>
/// Essential Complementary Filters:
/// </para>
/// <para>
/// RSI mean reversion should ALWAYS be combined with:
/// </para>
/// <list type="bullet">
///   <item><description>TrendFilter: Only take oversold signals in uptrends, overbought in downtrends (with-trend mean reversion)</description></item>
///   <item><description>Support/Resistance: Wait for RSI oversold + price at support level (confluence)</description></item>
///   <item><description>ADX filter: Only trade when ADX &lt; 25 (confirms ranging market)</description></item>
///   <item><description>Volume: Look for decreasing volume on the move into oversold (exhaustion)</description></item>
///   <item><description>Candlestick patterns: Wait for reversal candle (hammer, doji) at oversold level</description></item>
///   <item><description>Multiple timeframes: Check higher timeframe isn't in strong trend</description></item>
/// </list>
/// <para>
/// Exit Strategy:
/// </para>
/// <para>
/// Mean reversion exits are as important as entries:
/// </para>
/// <list type="bullet">
///   <item><description>Target RSI 50: Exit when RSI returns to neutral zone</description></item>
///   <item><description>Opposite extreme: Exit long when RSI reaches 70, exit short when RSI reaches 30</description></item>
///   <item><description>Support/Resistance: Take profit at next S/R level</description></item>
///   <item><description>Fixed R:R: Use 1:1 or 1:2 risk/reward ratio</description></item>
///   <item><description>Time-based: Exit after X bars if no mean reversion (timeout)</description></item>
/// </list>
/// <para>
/// Historical Context:
/// </para>
/// <para>
/// RSI was developed by J. Welles Wilder Jr. in 1978 and introduced in his book "New Concepts in Technical
/// Trading Systems." The 14-period and 30/70 thresholds were Wilder's recommendations based on commodities
/// markets in the 1970s. These parameters remain standard, though many traders adjust them for different
/// markets and timeframes. RSI is one of the most popular and widely-tested technical indicators, with
/// extensive academic research on its effectiveness (generally showing it works best in ranging markets).
/// </para>
/// </remarks>
public sealed class RSIReversionSignal : SignalBase
{
    private readonly int _period;
    private readonly decimal _oversold;
    private readonly decimal _overbought;

    /// <summary>
    /// Initializes a new instance of the <see cref="RSIReversionSignal"/> class with specified parameters.
    /// </summary>
    /// <param name="id">
    /// Unique identifier for this signal instance. Used for diagnostics and signal composition.
    /// Convention: lowercase with underscores (e.g., "rsi_reversion_14", "rsi_conservative").
    /// </param>
    /// <param name="period">
    /// Number of periods for RSI calculation. Determines indicator responsiveness.
    /// <list type="bullet">
    ///   <item><description>9: Fast, sensitive, more whipsaws (scalping)</description></item>
    ///   <item><description>14: Standard, balanced (Wilder's original)</description></item>
    ///   <item><description>21-25: Slow, smooth, fewer signals (swing trading)</description></item>
    /// </list>
    /// Default: 14 periods (Wilder's standard).
    /// Valid range: 5-30 periods.
    /// </param>
    /// <param name="oversold">
    /// RSI threshold below which a bullish reversal signal is generated.
    /// Lower values = rarer but stronger reversal signals.
    /// <list type="bullet">
    ///   <item><description>20: Conservative, only extreme oversold</description></item>
    ///   <item><description>30: Standard (Wilder's original)</description></item>
    ///   <item><description>35-40: Aggressive, more signals, use in ranging markets</description></item>
    /// </list>
    /// Default: 30 (standard).
    /// Valid range: 10-40.
    /// </param>
    /// <param name="overbought">
    /// RSI threshold above which a bearish reversal signal is generated.
    /// Higher values = rarer but stronger reversal signals.
    /// <list type="bullet">
    ///   <item><description>60-65: Aggressive, more signals, use in ranging markets</description></item>
    ///   <item><description>70: Standard (Wilder's original)</description></item>
    ///   <item><description>80: Conservative, only extreme overbought</description></item>
    /// </list>
    /// Default: 70 (standard).
    /// Valid range: 60-90.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if id is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown if period &lt; 5, period &gt; 30, oversold &lt; 10, oversold &gt; 40,
    /// overbought &lt; 60, overbought &gt; 90, or oversold &gt;= overbought.
    /// </exception>
    /// <example>
    /// <code>
    /// // Standard RSI mean reversion
    /// var signal = new RSIReversionSignal(
    ///     id: "rsi_standard",
    ///     period: 14,
    ///     oversold: 30m,
    ///     overbought: 70m
    /// );
    /// 
    /// // Conservative only-extreme approach
    /// var conservative = new RSIReversionSignal(
    ///     id: "rsi_conservative",
    ///     period: 14,
    ///     oversold: 20m,
    ///     overbought: 80m
    /// );
    /// </code>
    /// </example>
    public RSIReversionSignal(string id, int period = 14, decimal oversold = 30m, decimal overbought = 70m)
        : base(id, $"RSI-Reversion({period},{oversold:F0},{overbought:F0})")
    {
        if (period < 5 || period > 30)
            throw new ArgumentException("Period must be between 5 and 30", nameof(period));
        if (oversold < 10 || oversold > 40)
            throw new ArgumentException("Oversold threshold must be between 10 and 40", nameof(oversold));
        if (overbought < 60 || overbought > 90)
            throw new ArgumentException("Overbought threshold must be between 60 and 90", nameof(overbought));
        if (oversold >= overbought)
            throw new ArgumentException("Oversold must be less than overbought", nameof(oversold));

        _period = period;
        _oversold = oversold;
        _overbought = overbought;
    }

    /// <summary>
    /// Computes Relative Strength Index (RSI) using Wilder's smoothing method.
    /// </summary>
    /// <param name="prices">Closing prices for RSI calculation.</param>
    /// <param name="period">Number of periods for RSI calculation.</param>
    /// <returns>RSI value between 0 and 100, or 0 if insufficient data.</returns>
    /// <remarks>
    /// <para>
    /// Wilder's RSI formula:
    /// </para>
    /// <list type="number">
    ///   <item><description>Calculate gains (positive price changes) and losses (absolute negative changes)</description></item>
    ///   <item><description>First average gain/loss = Sum of gains/losses over period / period</description></item>
    ///   <item><description>Subsequent average gain = (Previous avg gain × (period-1) + current gain) / period</description></item>
    ///   <item><description>RS (Relative Strength) = Average Gain / Average Loss</description></item>
    ///   <item><description>RSI = 100 - (100 / (1 + RS))</description></item>
    /// </list>
    /// </remarks>
    private static decimal ComputeRSI(IReadOnlyList<decimal> prices, int period)
    {
        if (prices.Count < period + 1) return 0m;

        // Calculate price changes
        var gains = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < prices.Count; i++)
        {
            var change = prices[i] - prices[i - 1];
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? -change : 0);
        }

        if (gains.Count < period) return 0m;

        // Calculate initial average gain and loss
        decimal avgGain = gains.Take(period).Average();
        decimal avgLoss = losses.Take(period).Average();

        // Apply Wilder's smoothing for subsequent periods
        for (int i = period; i < gains.Count; i++)
        {
            avgGain = (avgGain * (period - 1) + gains[i]) / period;
            avgLoss = (avgLoss * (period - 1) + losses[i]) / period;
        }

        // Calculate RS and RSI
        if (avgLoss == 0) return 100m; // All gains, no losses

        decimal rs = avgGain / avgLoss;
        decimal rsi = 100m - (100m / (1m + rs));

        return rsi;
    }

    /// <summary>
    /// Core signal generation logic that evaluates RSI for overbought/oversold conditions.
    /// </summary>
    /// <param name="context">
    /// Immutable market context containing price history, indicators, and timestamp.
    /// Must contain at least (period + 2) candles for reliable RSI calculation.
    /// </param>
    /// <returns>
    /// <see cref="SignalResult"/> with:
    /// <list type="bullet">
    ///   <item><description>Long signal: When RSI is in oversold zone (below oversold threshold) with confidence based on depth</description></item>
    ///   <item><description>Short signal: When RSI is in overbought zone (above overbought threshold) with confidence based on depth</description></item>
    ///   <item><description>Neutral signal: When RSI is in neutral zone or insufficient data</description></item>
    /// </list>
    /// </returns>
    protected override SignalResult GenerateCore(IMarketContext context)
    {
        // Need period + 2 minimum: period for RSI, +1 for price change calculation, +1 for comparison
        if (context.Candles.Count < _period + 2)
            return NeutralResult(
                $"Insufficient data: need {_period + 2}, have {context.Candles.Count}",
                context.TimestampUtc);

        // Extract closing prices
        var closes = context.Candles.Select(c => c.Close).ToList();

        // Compute RSI
        var rsi = ComputeRSI(closes, _period);

        if (rsi == 0)
            return NeutralResult("RSI calculation failed", context.TimestampUtc);

        // Check for oversold condition (bullish mean reversion signal)
        if (rsi <= _oversold)
        {
            // Calculate confidence based on how deep into oversold zone
            // Deeper oversold = higher confidence
            var depth = _oversold - rsi;
            var maxDepth = _oversold; // Maximum possible depth (RSI could theoretically go to 0)
            var confidence = Math.Min(1.0, 0.5 + (double)(depth / maxDepth) * 0.5);

            return new SignalResult
            {
                Direction = SignalDirection.Long,
                Confidence = confidence,
                Reason = $"RSI oversold: RSI={rsi:F2}, Threshold={_oversold:F0}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["RSI"] = rsi,
                    ["OversoldThreshold"] = _oversold,
                    ["OverboughtThreshold"] = _overbought,
                    ["Depth"] = depth,
                    ["Period"] = _period,
                    ["Severity"] = rsi < 20 ? "Extreme" : rsi < 25 ? "Strong" : "Moderate"
                }
            };
        }

        // Check for overbought condition (bearish mean reversion signal)
        if (rsi >= _overbought)
        {
            // Calculate confidence based on how deep into overbought zone
            // Deeper overbought = higher confidence
            var depth = rsi - _overbought;
            var maxDepth = 100m - _overbought; // Maximum possible depth (RSI could theoretically go to 100)
            var confidence = Math.Min(1.0, 0.5 + (double)(depth / maxDepth) * 0.5);

            return new SignalResult
            {
                Direction = SignalDirection.Short,
                Confidence = confidence,
                Reason = $"RSI overbought: RSI={rsi:F2}, Threshold={_overbought:F0}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["RSI"] = rsi,
                    ["OversoldThreshold"] = _oversold,
                    ["OverboughtThreshold"] = _overbought,
                    ["Depth"] = depth,
                    ["Period"] = _period,
                    ["Severity"] = rsi > 80 ? "Extreme" : rsi > 75 ? "Strong" : "Moderate"
                }
            };
        }

        // RSI in neutral zone - no signal
        return NeutralResult(
            $"RSI neutral: RSI={rsi:F2} within neutral zone [{_oversold:F0}, {_overbought:F0}]",
            context.TimestampUtc);
    }
}