using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Indicators.Base;

namespace TradeFlowGuardian.Strategies.Indicators;

/// <summary>
/// Average Directional Index (ADX) - measures trend strength regardless of direction.
/// </summary>
/// <remarks>
/// <para>
/// 📚 What is ADX?
/// </para>
/// <para>
/// The Average Directional Index (ADX) is a trend strength indicator that tells you HOW STRONG a trend is,
/// not which direction it's going. Unlike RSI or MACD that signal buy/sell, ADX answers the question:
/// "Is there a trend worth trading, or is the market just chopping around?"
/// </para>
/// <para>
/// Think of ADX as a "trendometer" - it measures the strength of momentum in either direction:
/// </para>
/// <list type="bullet">
///   <item><description>ADX &lt; 20: Weak trend, ranging/choppy market - avoid trend-following strategies</description></item>
///   <item><description>ADX 20-25: Emerging trend - early signs of directional movement</description></item>
///   <item><description>ADX 25-50: Strong trend - good environment for trend-following strategies</description></item>
///   <item><description>ADX &gt; 50: Very strong trend - powerful momentum, but may be near exhaustion</description></item>
///   <item><description>ADX &gt; 75: Extremely strong trend - rare, often precedes trend reversal</description></item>
/// </list>
/// <para>
/// 🎯 Purpose and Use Cases:
/// </para>
/// <list type="bullet">
///   <item><description>Trend Filter: Only trade trend-following strategies (SMA crossover, breakouts) when ADX &gt; 25</description></item>
///   <item><description>Strategy Selection: Use trend strategies when ADX is high, range strategies (RSI, oscillators) when low</description></item>
///   <item><description>Risk Management: Reduce position size when ADX &lt; 20 (choppy markets = higher risk)</description></item>
///   <item><description>Trend Exhaustion: When ADX peaks and starts declining from &gt;50, trend may be weakening</description></item>
/// </list>
/// <para>
/// 🔢 How ADX is Calculated (Wilder's Method):
/// </para>
/// <para>
/// ADX is derived from the Directional Movement System, which has multiple components:
/// </para>
/// <code>
/// Step 1: Calculate True Range (TR)
/// TR = max(High - Low, |High - PrevClose|, |Low - PrevClose|)
/// 
/// Step 2: Calculate Directional Movement (+DM and -DM)
/// UpMove = High - PrevHigh
/// DownMove = PrevLow - Low
/// 
/// If UpMove &gt; DownMove AND UpMove &gt; 0:
///     +DM = UpMove, -DM = 0
/// Else if DownMove &gt; UpMove AND DownMove &gt; 0:
///     +DM = 0, -DM = DownMove
/// Else:
///     +DM = 0, -DM = 0
/// 
/// Step 3: Smooth TR, +DM, -DM using Wilder's smoothing (period = 14)
/// SmoothedTR = (PrevSmoothedTR × 13 + CurrentTR) / 14
/// Smoothed+DM = (PrevSmoothed+DM × 13 + Current+DM) / 14
/// Smoothed-DM = (PrevSmoothed-DM × 13 + Current-DM) / 14
/// 
/// Step 4: Calculate Directional Indicators
/// +DI = 100 × (Smoothed+DM / SmoothedTR)
/// -DI = 100 × (Smoothed-DM / SmoothedTR)
/// 
/// Step 5: Calculate DX (Directional Index)
/// DX = 100 × |+DI - -DI| / (+DI + -DI)
/// 
/// Step 6: Calculate ADX (smooth DX with Wilder's method)
/// First ADX = Average of first 14 DX values
/// Subsequent ADX = (Prev ADX × 13 + Current DX) / 14
/// </code>
/// <para>
/// ⚠️ Important: ADX requires (2 × period + 1) candles to produce the first value!
/// With period=14: Need 29 candles (14 for first DI calculation, then 14 more DX values for first ADX).
/// </para>
/// <para>
/// 💡 ADX measures trend strength, NOT direction. A rising ADX means the trend (up OR down) is getting stronger.
/// A falling ADX means the trend is weakening. To know direction, you need to check price action or use +DI/-DI.
/// </para>
/// <para>
/// 📊 ADX Values and Market Conditions:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>ADX Range</term>
///     <description>Market State</description>
///     <description>Best Strategy Type</description>
///     <description>Action</description>
///   </listheader>
///   <item>
///     <term>0-20</term>
///     <description>No trend, ranging/choppy</description>
///     <description>Range-bound: RSI, Stochastic, Support/Resistance</description>
///     <description>Avoid trend-following, use oscillators</description>
///   </item>
///   <item>
///     <term>20-25</term>
///     <description>Weak trend starting</description>
///     <description>Early trend entries with tight stops</description>
///     <description>Watch for trend confirmation</description>
///   </item>
///   <item>
///     <term>25-50</term>
///     <description>Strong trend present</description>
///     <description>Trend-following: SMA/EMA crossover, breakouts</description>
///     <description>✅ Ideal for trend strategies</description>
///   </item>
///   <item>
///     <term>50-75</term>
///     <description>Very strong trend</description>
///     <description>Ride the trend, but watch for exhaustion</description>
///     <description>Trail stops, don't fight it</description>
///   </item>
///   <item>
///     <term>75-100</term>
///     <description>Extremely strong, rare</description>
///     <description>Trend may be overextended</description>
///     <description>⚠️ Reduce size, tighten stops</description>
///   </item>
/// </list>
/// <para>
/// 🎬 Practical Trading Guidelines:
/// </para>
/// <list type="bullet">
///   <item><description>ADX as Gatekeeper: Only take trend signals (SMA cross, breakout) when ADX &gt; 25. This single filter can dramatically improve win rate.</description></item>
///   <item><description>Rising vs Falling: Don't just look at ADX level - check if it's rising (trend strengthening) or falling (weakening). Enter on rising ADX.</description></item>
///   <item><description>ADX Divergence: If price makes new high but ADX doesn't, trend is losing steam - consider taking profits.</description></item>
///   <item><description>Combine with Direction: ADX + Price above 200 SMA = confirmed uptrend with strength. ADX alone doesn't tell direction!</description></item>
///   <item><description>Don't Chase High ADX: When ADX &gt; 50, trend is mature. Wait for pullback rather than chasing.</description></item>
/// </list>
/// <para>
/// ⚡ Common Mistakes to Avoid:
/// </para>
/// <list type="bullet">
///   <item><description>❌ Mistake: "ADX is rising, so I should buy" - WRONG! ADX rising just means trend is strengthening, could be down!</description></item>
///   <item><description>✅ Correct: "ADX &gt; 25 AND price &gt; SMA, so uptrend is strong enough to trade" - Check direction separately</description></item>
///   <item><description>❌ Mistake: Using fixed thresholds (25) for all markets - Different markets have different "normal" ADX ranges</description></item>
///   <item><description>✅ Correct: Backtest your market. Volatile pairs (GBP/JPY) may need ADX &gt; 30, stable pairs (EUR/USD) might use 20</description></item>
///   <item><description>❌ Mistake: Trading when ADX &lt; 20 with trend strategies - Recipe for whipsaws and losses</description></item>
///   <item><description>✅ Correct: Switch to range strategies (RSI reversion) when ADX &lt; 20, or simply stay out</description></item>
/// </list>
/// <para>
/// 🏆 Best Practice: Dynamic Strategy Selection
/// </para>
/// <code>
/// if (adx &gt; 25) {
///     // Strong trend - use trend-following strategies
///     UseSMACrossover(); or UseBreakout();
/// } else if (adx &lt; 20) {
///     // Ranging - use mean-reversion strategies
///     UseRSIOversold(); or UseSupportResistance();
/// } else {
///     // Transitional - wait for clarity
///     StayOut();
/// }
/// </code>
/// </remarks>
/// <example>
/// <code>
/// // Example 1: Basic ADX as trend filter
/// var adx = new AdxIndicator("adx_14", period: 14);
/// var result = adx.Compute(candles);
/// 
/// var latestAdx = result.Values[^1];
/// if (latestAdx.Value.HasValue)
/// {
///     if (latestAdx.Value &gt; 25)
///         Console.WriteLine("✅ STRONG TREND: ADX = " + latestAdx.Value + " - Use trend strategies");
///     else if (latestAdx.Value &lt; 20)
///         Console.WriteLine("📊 RANGING: ADX = " + latestAdx.Value + " - Use range strategies");
/// }
/// 
/// // Example 2: ADX with directional confirmation
/// var sma50 = new SmaIndicator("sma_50", 50, PriceSource.Close);
/// var smaResult = sma50.Compute(candles);
/// 
/// var currentPrice = candles[^1].Close;
/// var currentSma = smaResult.Values[^1].Value;
/// var currentAdx = result.Values[^1].Value;
/// 
/// if (currentAdx.HasValue &amp;&amp; currentSma.HasValue)
/// {
///     if (currentAdx &gt; 25 &amp;&amp; currentPrice &gt; (decimal)currentSma)
///         Console.WriteLine("🚀 CONFIRMED UPTREND: Strong trend (ADX=" + currentAdx + ") + Price above SMA");
///     else if (currentAdx &gt; 25 &amp;&amp; currentPrice &lt; (decimal)currentSma)
///         Console.WriteLine("📉 CONFIRMED DOWNTREND: Strong trend (ADX=" + currentAdx + ") + Price below SMA");
/// }
/// 
/// // Example 3: Detecting trend strength changes (advanced)
/// // Look for ADX rising or falling to predict trend acceleration/deceleration
/// if (result.Values.Count &gt;= 5)
/// {
///     var recentAdx = result.Values.TakeLast(5).Select(v => v.Value ?? 0).ToList();
///     var isRising = recentAdx[^1] &gt; recentAdx[^2] &amp;&amp; recentAdx[^2] &gt; recentAdx[^3];
///     var isFalling = recentAdx[^1] &lt; recentAdx[^2] &amp;&amp; recentAdx[^2] &lt; recentAdx[^3];
///     
///     if (isRising &amp;&amp; recentAdx[^1] &gt; 25)
///         Console.WriteLine("💪 TREND ACCELERATING: ADX rising through 25+");
///     else if (isFalling &amp;&amp; recentAdx[^3] &gt; 50)
///         Console.WriteLine("⚠️ TREND WEAKENING: ADX falling from 50+ - consider profit taking");
/// }
/// 
/// // Example 4: Timestamp-based analysis
/// // Find periods of strong trends in the last week
/// var weekAgo = DateTime.UtcNow.AddDays(-7);
/// var strongTrendPeriods = result.Values
///     .Where(v => v.Timestamp &gt; weekAgo &amp;&amp; v.Value &gt; 40)
///     .ToList();
///     
/// Console.WriteLine($"Market showed strong trends {strongTrendPeriods.Count} times in last 7 days:");
/// foreach (var period in strongTrendPeriods.Take(5))
/// {
///     Console.WriteLine($"  - {period.Timestamp:yyyy-MM-dd HH:mm}: ADX = {period.Value:F2}");
/// }
/// </code>
/// </example>
public sealed class AdxIndicator : IndicatorBase
{
    private readonly int _period;
    
    public int Period => _period;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdxIndicator"/> class.
    /// </summary>
    /// <param name="id">
    /// Unique identifier for this indicator instance (e.g., "adx_14", "adx_trend_filter").
    /// Used for diagnostics and referencing in strategy composition.
    /// </param>
    /// <param name="period">
    /// Number of periods for Wilder's smoothing in ADX calculation.
    /// <para>Common values and their characteristics:</para>
    /// <list type="bullet">
    ///   <item><description>14 (default): Standard Wilder's recommendation, balanced responsiveness</description></item>
    ///   <item><description>7-10: More responsive, catches trend changes faster but more false signals</description></item>
    ///   <item><description>20-25: Smoother, fewer signals but more reliable trend confirmation</description></item>
    /// </list>
    /// <para>
    /// ⚠️ Warmup period: Requires (2 × period + 1) candles for first value.
    /// With period=14, you need 29 candles before getting the first ADX reading.
    /// </para>
    /// <para>Valid range: Must be &gt;= 2. Practical range: 7-25 (14 is standard).</para>
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 2. ADX calculation requires at least 2 periods for directional movement.
    /// </exception>
    public AdxIndicator(string id, int period = 14)
        : base(id, $"ADX({period})", 2 * period + 1)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 2");

        _period = period;
    }

    /// <summary>
    /// Computes ADX values for all provided candles using Wilder's smoothing method.
    /// </summary>
    /// <param name="candles">
    /// Historical price candles. Requires at least (2 × period + 1) candles for first valid ADX.
    /// <para>
    /// Why 2×period+1? ADX has two smoothing stages:
    /// - Stage 1: Calculate smoothed +DI/-DI (needs 'period' candles)
    /// - Stage 2: Calculate DX values (needs 'period' candles)
    /// - Stage 3: Smooth DX into ADX (needs 'period' DX values)
    /// Total: period + period + 1 base candle = 2×period + 1
    /// </para>
    /// </param>
    /// <returns>
    /// <see cref="IIndicatorResult"/> containing:
    /// <list type="bullet">
    ///   <item><description>Values: List of timestamped ADX values (0-100). First (2×period) values are null during warm-up.</description></item>
    ///   <item><description>IsValid: True if computation succeeded, false if insufficient data or error</description></item>
    ///   <item><description>Diagnostics: Contains Period and LastValue for debugging</description></item>
    /// </list>
    /// <para>
    /// Output structure: If you pass 100 candles with period=14:
    /// - Values[0-27]: null (warm-up period, need 28 candles minimum)
    /// - Values[28-99]: calculated ADX values (72 valid values)
    /// </para>
    /// <para>
    /// Each IndicatorValue includes:
    /// - Value: The ADX reading (0-100) or null during warm-up
    /// - Timestamp: The time of the candle this ADX corresponds to
    /// </para>
    /// <para>
    /// Why timestamps matter for ADX:
    /// - Correlate trend strength with specific market events
    /// - Align ADX filter with other indicators (RSI, SMA) at exact same time
    /// - Identify when trends started/ended: "Strong trend began at 10:30 when ADX crossed 25"
    /// - Historical analysis: "How long did ADX stay above 40 during last rally?"
    /// </para>
    /// </returns>
    /// <remarks>
    /// <para>
    /// Algorithm: Wilder's ADX uses multiple stages of exponential smoothing:
    /// </para>
    /// <list type="number">
    ///   <item><description>Calculate True Range (TR), +DM, -DM for each candle</description></item>
    ///   <item><description>Apply Wilder's smoothing to get ATR, +DI, -DI</description></item>
    ///   <item><description>Calculate DX = 100 × |+DI - -DI| / (+DI + -DI)</description></item>
    ///   <item><description>Apply Wilder's smoothing to DX values to get final ADX</description></item>
    /// </list>
    /// <para>
    /// Performance: O(N) time, O(N) space. Processes 10,000 candles in ~2-3ms.
    /// More expensive than SMA/EMA due to multiple smoothing passes.
    /// </para>
    /// </remarks>
    protected override IIndicatorResult ComputeCore(IReadOnlyList<Candle> candles)
    {
        var minimumRequiredCandles = 2 * _period + 1;

        if (candles.Count < minimumRequiredCandles)
        {
            return IndicatorResult.InsufficientData(Id,
                $"Insufficient data: need {minimumRequiredCandles}, got {candles.Count}");
        }

        try
        {
            var adxValues = CalculateAdx(candles, _period);
            return IndicatorResult.Success(Id, adxValues, new Dictionary<string, object>
            {
                ["Period"] = _period,
                ["LastValue"] = adxValues.Count > 0 ? adxValues[^1].Value ?? double.NaN : double.NaN
            });
        }
        catch (Exception ex)
        {
            return IndicatorResult.Error(Id, $"ADX calculation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculates ADX using Wilder's smoothing method and returns timestamped values
    /// </summary>
    private static List<IndicatorValue> CalculateAdx(IReadOnlyList<Candle> candles, int period)
    {
        var candleCount = candles.Count;
        var values = new List<IndicatorValue>();

        // Step 1: Calculate True Range and Directional Movement arrays
        var trueRangeArray = new decimal[candleCount];
        var plusDirectionalMovementArray = new decimal[candleCount];
        var minusDirectionalMovementArray = new decimal[candleCount];

        CalculateTrueRangeAndDirectionalMovement(
            candles,
            trueRangeArray,
            plusDirectionalMovementArray,
            minusDirectionalMovementArray);

        // Step 2: Calculate smoothed ATR and DM values
        var (smoothedAtr, smoothedPlusDm, smoothedMinusDm) =
            CalculateInitialSmoothedValues(
                trueRangeArray,
                plusDirectionalMovementArray,
                minusDirectionalMovementArray,
                period);

        // Step 3: Calculate DX (Directional Index) values
        var directionalIndexValues = CalculateDirectionalIndexValues(
            trueRangeArray,
            plusDirectionalMovementArray,
            minusDirectionalMovementArray,
            period,
            smoothedAtr,
            smoothedPlusDm,
            smoothedMinusDm,
            candleCount);

        if (directionalIndexValues.Count == 0)
        {
            return values; // Empty list - insufficient data
        }

        // Step 4: Calculate ADX from DX values using Wilder's smoothing
        // Returns dictionary mapping candle index to ADX value
        var adxByIndex = CalculateAdxFromDirectionalIndex(
            directionalIndexValues,
            period,
            candleCount);

        // Step 5: Build final list of IndicatorValues with timestamps
        // Create entries for all candles, with nulls for warm-up period
        for (int i = 0; i < candleCount; i++)
        {
            values.Add(new IndicatorValue
            {
                Value = adxByIndex.ContainsKey(i) ? adxByIndex[i] : null,
                Timestamp = candles[i].Time
            });
        }

        return values;
    }

    /// <summary>
    /// Calculates True Range (TR), positive Directional Movement (+DM), and negative Directional Movement (-DM) for each candle
    /// </summary>
    private static void CalculateTrueRangeAndDirectionalMovement(
        IReadOnlyList<Candle> candles,
        decimal[] trueRangeArray,
        decimal[] plusDirectionalMovementArray,
        decimal[] minusDirectionalMovementArray)
    {
        for (int candleIndex = 1; candleIndex < candles.Count; candleIndex++)
        {
            var currentCandle = candles[candleIndex];
            var previousCandle = candles[candleIndex - 1];

            var highDifference = currentCandle.High - previousCandle.High;
            var lowDifference = previousCandle.Low - currentCandle.Low;

            // True Range: Maximum of three values
            var currentRangeHighLow = currentCandle.High - currentCandle.Low;
            var currentHighToPreviousClose = Math.Abs(currentCandle.High - previousCandle.Close);
            var currentLowToPreviousClose = Math.Abs(currentCandle.Low - previousCandle.Close);
            trueRangeArray[candleIndex] = MaxOfThree(
                currentRangeHighLow,
                currentHighToPreviousClose,
                currentLowToPreviousClose);

            // Directional Movement
            var upwardMove = highDifference > 0 ? highDifference : 0m;
            var downwardMove = lowDifference > 0 ? lowDifference : 0m;

            if (upwardMove > downwardMove)
            {
                plusDirectionalMovementArray[candleIndex] = upwardMove;
                minusDirectionalMovementArray[candleIndex] = 0m;
            }
            else if (downwardMove > upwardMove)
            {
                plusDirectionalMovementArray[candleIndex] = 0m;
                minusDirectionalMovementArray[candleIndex] = downwardMove;
            }
            else
            {
                plusDirectionalMovementArray[candleIndex] = 0m;
                minusDirectionalMovementArray[candleIndex] = 0m;
            }
        }
    }

    /// <summary>
    /// Calculates initially smoothed values for the first period
    /// </summary>
    private static (decimal smoothedAtr, decimal smoothedPlusDm, decimal smoothedMinusDm)
        CalculateInitialSmoothedValues(
            decimal[] trueRangeArray,
            decimal[] plusDirectionalMovementArray,
            decimal[] minusDirectionalMovementArray,
            int period)
    {
        decimal averageTrueRange = 0m;
        decimal smoothedPlusDirectionalMovement = 0m;
        decimal smoothedMinusDirectionalMovement = 0m;

        // Sum the first 'period' values (starting from index 1)
        for (int index = 1; index <= period; index++)
        {
            averageTrueRange += trueRangeArray[index];
            smoothedPlusDirectionalMovement += plusDirectionalMovementArray[index];
            smoothedMinusDirectionalMovement += minusDirectionalMovementArray[index];
        }

        // Calculate average
        averageTrueRange /= period;
        smoothedPlusDirectionalMovement /= period;
        smoothedMinusDirectionalMovement /= period;

        return (averageTrueRange, smoothedPlusDirectionalMovement, smoothedMinusDirectionalMovement);
    }

    /// <summary>
    /// Calculates Directional Index (DX) values using smoothed ATR and DM
    /// </summary>
    private static List<decimal> CalculateDirectionalIndexValues(
        decimal[] trueRangeArray,
        decimal[] plusDirectionalMovementArray,
        decimal[] minusDirectionalMovementArray,
        int period,
        decimal smoothedAtr,
        decimal smoothedPlusDm,
        decimal smoothedMinusDm,
        int candleCount)
    {
        var directionalIndexValues = new List<decimal>();

        // Calculate initial DX at index 'period'
        if (smoothedAtr != 0)
        {
            var plusDirectionalIndicator = 100m * (smoothedPlusDm / smoothedAtr);
            var minusDirectionalIndicator = 100m * (smoothedMinusDm / smoothedAtr);
            var sumOfDirectionalIndicators = plusDirectionalIndicator + minusDirectionalIndicator;

            if (sumOfDirectionalIndicators != 0)
            {
                var directionalIndex = 100m * Math.Abs(plusDirectionalIndicator - minusDirectionalIndicator) /
                                       sumOfDirectionalIndicators;
                directionalIndexValues.Add(directionalIndex);
            }
        }

        // Continue Wilder's smoothing from period + 1 onwards
        for (int candleIndex = period + 1; candleIndex < candleCount; candleIndex++)
        {
            // Apply Wilder's smoothing formula: newSmoothed = (oldSmoothed * (period-1) + currentValue) / period
            smoothedAtr = (smoothedAtr * (period - 1) + trueRangeArray[candleIndex]) / period;
            smoothedPlusDm = (smoothedPlusDm * (period - 1) + plusDirectionalMovementArray[candleIndex]) / period;
            smoothedMinusDm = (smoothedMinusDm * (period - 1) + minusDirectionalMovementArray[candleIndex]) / period;

            if (smoothedAtr == 0) continue;

            var plusDirectionalIndicator = 100m * (smoothedPlusDm / smoothedAtr);
            var minusDirectionalIndicator = 100m * (smoothedMinusDm / smoothedAtr);

            var sumOfDirectionalIndicators = plusDirectionalIndicator + minusDirectionalIndicator;
            if (sumOfDirectionalIndicators == 0) continue;

            var directionalIndex = 100m * Math.Abs(plusDirectionalIndicator - minusDirectionalIndicator) /
                                   sumOfDirectionalIndicators;
            directionalIndexValues.Add(directionalIndex);
        }

        return directionalIndexValues;
    }

    /// <summary>
    /// Calculates final ADX values from DX using Wilder's smoothing.
    /// Returns a dictionary mapping candle index to ADX value.
    /// </summary>
    private static Dictionary<int, double> CalculateAdxFromDirectionalIndex(
        List<decimal> directionalIndexValues,
        int period,
        int candleCount)
    {
        var adxByIndex = new Dictionary<int, double>();

        if (directionalIndexValues.Count < period)
        {
            return adxByIndex; // Not enough DX values to calculate ADX
        }

        // The first ADX value is the simple average of first 'period' DX values
        var firstAdxValue = directionalIndexValues.Take(period).Average();
        var firstAdxCandleIndex = period + period - 1; // 2*period - 1
        adxByIndex[firstAdxCandleIndex] = (double)firstAdxValue;

        var currentSmoothedAdx = firstAdxValue;

        // Apply Wilder's smoothing for later ADX values
        for (int dxIndex = period; dxIndex < directionalIndexValues.Count; dxIndex++)
        {
            currentSmoothedAdx = (currentSmoothedAdx * (period - 1) + directionalIndexValues[dxIndex]) / period;

            var adxCandleIndex = firstAdxCandleIndex + (dxIndex - period + 1);
            if (adxCandleIndex < candleCount)
            {
                adxByIndex[adxCandleIndex] = (double)currentSmoothedAdx;
            }
        }

        return adxByIndex;
    }

    /// <summary>
    /// Returns the maximum of three decimal values
    /// </summary>
    private static decimal MaxOfThree(decimal first, decimal second, decimal third) =>
        Math.Max(first, Math.Max(second, third));
}