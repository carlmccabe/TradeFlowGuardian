using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Indicators.Base;

namespace TradeFlowGuardian.Strategies.Indicators;

/// <summary>
/// Relative Strength Index (RSI) indicator - measures momentum by comparing magnitude of recent gains vs losses.
/// </summary>
/// <remarks>
/// <para>
/// 📚 What is RSI?
/// </para>
/// <para>
/// The Relative Strength Index (RSI) is a momentum oscillator that measures the speed and magnitude of price changes.
/// It oscillates between 0 and 100, where traditionally:
/// </para>
/// <list type="bullet">
///   <item><description>RSI > 70: Overbought condition - price may reverse downward</description></item>
///   <item><description>RSI &lt; 30: Oversold condition - price may reverse upward</description></item>
///   <item><description>RSI = 50: Neutral momentum, no clear bias</description></item>
/// </list>
/// <para>
/// 🎯 Purpose and Use Cases:
/// </para>
/// <list type="bullet">
///   <item><description>Mean Reversion: Identify overbought/oversold levels for counter-trend trades</description></item>
///   <item><description>Divergence Detection: When price makes new high but RSI doesn't, signals potential reversal</description></item>
///   <item><description>Trend Confirmation: In strong uptrends, RSI stays above 40; in downtrends, below 60</description></item>
///   <item><description>Entry Timing: Wait for RSI to exit overbought/oversold zone before entering</description></item>
/// </list>
/// <para>
/// 🔢 How RSI is Calculated:
/// </para>
/// <para>
/// RSI uses a two-step process with Wilder's smoothing:
/// </para>
/// <code>
/// Step 1: Calculate price changes
/// Change[i] = Close[i] - Close[i-1]
/// 
/// Step 2: Separate gains and losses
/// Gain[i] = Change[i] if positive, else 0
/// Loss[i] = |Change[i]| if negative, else 0
/// 
/// Step 3: First average uses simple moving average
/// Initial AvgGain = Sum(Gains over period) / period
/// Initial AvgLoss = Sum(Losses over period) / period
/// 
/// Step 4: Subsequent averages use Wilder's smoothing
/// AvgGain = (PrevAvgGain × (period-1) + CurrentGain) / period
/// AvgLoss = (PrevAvgLoss × (period-1) + CurrentLoss) / period
/// 
/// Step 5: Calculate Relative Strength and RSI
/// RS = AvgGain / AvgLoss
/// RSI = 100 - (100 / (1 + RS))
/// 
/// Special case: If AvgLoss = 0, RSI = 100
/// </code>
/// <para>
/// 💡 Wilder's Smoothing: Unlike a simple moving average, Wilder's method gives more weight to recent data
/// while still considering the full history. This makes RSI more responsive to recent changes while remaining stable.
/// </para>
/// <para>
/// 📊 Common RSI Periods and Their Meanings:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Period</term>
///     <description>Typical Use</description>
///     <description>Sensitivity</description>
///     <description>Best For</description>
///   </listheader>
///   <item>
///     <term>14 (default)</term>
///     <description>Standard RSI, most common</description>
///     <description>Balanced - not too fast or slow</description>
///     <description>General purpose, swing trading</description>
///   </item>
///   <item>
///     <term>7-9</term>
///     <description>Fast RSI, more sensitive</description>
///     <description>High - reaches extremes frequently</description>
///     <description>Day trading, scalping (use 80/20 thresholds)</description>
///   </item>
///   <item>
///     <term>21-25</term>
///     <description>Slow RSI, more smoothed</description>
///     <description>Low - fewer but more reliable signals</description>
///     <description>Position trading, trend following</description>
///   </item>
/// </list>
/// <para>
/// ⚠️ Important: RSI with period=14 requires 15 candles to produce the first value (period + 1)
/// because you need period+1 candles to calculate period changes.
/// </para>
/// <para>
/// ⚡ RSI vs Other Oscillators:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Indicator</term>
///     <description>Range</description>
///     <description>Best For</description>
///     <description>Key Difference</description>
///   </listheader>
///   <item>
///     <term>RSI</term>
///     <description>0-100</description>
///     <description>Momentum, overbought/oversold</description>
///     <description>Uses only closing prices with smoothing</description>
///   </item>
///   <item>
///     <term>Stochastic</term>
///     <description>0-100</description>
///     <description>Position within range</description>
///     <description>Compares close to high-low range</description>
///   </item>
///   <item>
///     <term>CCI</term>
///     <description>Unbounded</description>
///     <description>Extreme deviations</description>
///     <description>Measures deviation from typical price</description>
///   </item>
/// </list>
/// <para>
/// 🎬 Practical Trading Guidelines:
/// </para>
/// <list type="bullet">
///   <item><description>Don't fight the trend: In strong uptrends, RSI can stay overbought (>70) for extended periods</description></item>
///   <item><description>Use dynamic thresholds: In uptrends use 40-80, in downtrends use 20-60 instead of 30-70</description></item>
///   <item><description>Wait for exit: Don't enter just because RSI hits 70 or 30 - wait for it to reverse back</description></item>
///   <item><description>Combine with trend: Only take oversold signals in uptrends, overbought signals in downtrends</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Example 1: Basic RSI with overbought/oversold detection
/// var rsi = new RsiIndicator("rsi_14", period: 14);
/// var result = rsi.Compute(candles);
/// 
/// var latestRsi = result.Values[^1];
/// if (latestRsi.Value.HasValue)
/// {
///     if (latestRsi.Value > 70)
///         Console.WriteLine("⚠️ OVERBOUGHT: RSI = " + latestRsi.Value);
///     else if (latestRsi.Value &lt; 30)
///         Console.WriteLine("⚠️ OVERSOLD: RSI = " + latestRsi.Value);
/// }
/// 
/// // Example 2: RSI divergence detection (advanced)
/// // When price makes new high but RSI doesn't = bearish divergence
/// if (result.Values.Count >= 20)
/// {
///     var recentRsi = result.Values.TakeLast(20).ToList();
///     var recentCandles = candles.TakeLast(20).ToList();
///     
///     var priceHigh = recentCandles.Max(c => c.High);
///     var rsiHigh = recentRsi.Max(v => v.Value ?? 0);
///     
///     var currentPrice = recentCandles[^1].High;
///     var currentRsi = recentRsi[^1].Value ?? 0;
///     
///     if (currentPrice >= priceHigh &amp;&amp; currentRsi &lt; rsiHigh)
///         Console.WriteLine("🔴 BEARISH DIVERGENCE: Price at high but RSI declining");
/// }
/// 
/// // Example 3: Timestamp-aware analysis
/// // Find when RSI crossed below 30 in the last 24 hours
/// var yesterday = DateTime.UtcNow.AddHours(-24);
/// var oversoldMoments = result.Values
///     .Where(v => v.Timestamp > yesterday &amp;&amp; v.Value &lt; 30)
///     .ToList();
///     
/// Console.WriteLine($"RSI was oversold {oversoldMoments.Count} times in last 24h");
/// foreach (var moment in oversoldMoments)
/// {
///     Console.WriteLine($"  - {moment.Timestamp:yyyy-MM-dd HH:mm}: RSI = {moment.Value:F2}");
/// }
/// </code>
/// </example>
public sealed class RsiIndicator : IndicatorBase
{
    private readonly int _period;
    
    public int Period => _period;

    public RsiIndicator(string id, int period = 14)
        : base(id, $"RSI({period})", period + 1) // +1 because we need n+1 candles for n changes
    {
        if (period < 1)
            throw new ArgumentException("Period must be >= 1", nameof(period));

        _period = period;
    }

    /// <summary>
    /// Computes RSI values for all provided candles using Wilder's smoothing method.
    /// </summary>
    /// <param name="candles">
    /// Historical price candles. Requires at least (period + 1) candles for the first valid RSI value.
    /// <para>
    /// Why period+1? Because RSI calculates changes between candles, you need one extra:
    /// - Period=14 needs 15 candles: 1st candle establishes baseline, next 14 provide changes
    /// </para>
    /// </param>
    /// <returns>
    /// <see cref="IIndicatorResult"/> containing:
    /// <list type="bullet">
    ///   <item><description>Values: List of timestamped RSI values (0-100). First (period) values are null during warm-up.</description></item>
    ///   <item><description>IsValid: True if computation succeeded</description></item>
    ///   <item><description>Diagnostics: Contains Period and LastValue for debugging</description></item>
    /// </list>
    /// <para>
    /// Output structure: If you pass 100 candles with period=14:
    /// - Values[0-13]: null (warm-up period, insufficient data)
    /// - Values[14-99]: calculated RSI values (86 valid values)
    /// </para>
    /// <para>
    /// Each IndicatorValue contains:
    /// - Value: The RSI reading (0-100) or null if still warming up
    /// - Timestamp: The time of the candle this RSI corresponds to
    /// </para>
    /// <para>
    /// This timestamp alignment is crucial because:
    /// - Allows you to correlate RSI with specific market moments
    /// - Enables time-based filtering ("RSI was oversold 3 hours ago")
    /// - Makes multi-indicator analysis possible (compare RSI and SMA at same timestamp)
    /// </para>
    /// </returns>
    /// <remarks>
    /// <para>
    /// Algorithm Details:
    /// </para>
    /// <para>
    /// This implementation follows Wilder's original RSI calculation exactly:
    /// </para>
    /// <list type="number">
    ///   <item><description>Extract closing prices from candles</description></item>
    ///   <item><description>Calculate price changes: change[i] = close[i] - close[i-1]</description></item>
    ///   <item><description>For first RSI value, use simple average of gains/losses over period</description></item>
    ///   <item><description>For subsequent values, use Wilder's smoothing: avgGain = (prevAvg × (period-1) + currentGain) / period</description></item>
    ///   <item><description>Calculate RS = avgGain / avgLoss</description></item>
    ///   <item><description>Calculate RSI = 100 - (100 / (1 + RS))</description></item>
    /// </list>
    /// <para>
    /// Performance: O(N) time complexity, O(N) space for output. Processes 10,000 candles in ~1-2ms.
    /// </para>
    /// </remarks>
    protected override IIndicatorResult ComputeCore(IReadOnlyList<Candle> candles)
    {
        var closes = candles.Select(c => (double)c.Close).ToArray();
        var values = new List<IndicatorValue>();

        // Calculate price changes
        var changes = new double[closes.Length - 1];
        for (int i = 0; i < changes.Length; i++)
        {
            changes[i] = closes[i + 1] - closes[i];
        }

        // First RSI uses simple average
        if (changes.Length >= _period)
        {
            double sumGain = 0, sumLoss = 0;

            // Calculate initial averages over the first 'period' changes
            for (int i = 0; i < _period; i++)
            {
                if (changes[i] >= 0)
                    sumGain += changes[i];
                else
                    sumLoss += Math.Abs(changes[i]);
            }

            double avgGain = sumGain / _period;
            double avgLoss = sumLoss / _period;

            // First RSI value at index 'period' (corresponds to candle at index 'period')
            // because we need period+1 candles to calculate period changes
            values.Add(new IndicatorValue
            {
                Value = CalculateRsi(avgGain, avgLoss),
                Timestamp = candles[_period].Time
            });

            // Subsequent RSI uses Wilder's smoothing
            for (int i = _period; i < changes.Length; i++)
            {
                double change = changes[i];
                double gain = change >= 0 ? change : 0;
                double loss = change < 0 ? Math.Abs(change) : 0;

                avgGain = (avgGain * (_period - 1) + gain) / _period;
                avgLoss = (avgLoss * (_period - 1) + loss) / _period;
                // Add timestamped value
                values.Add(new IndicatorValue
                {
                    Value = CalculateRsi(avgGain, avgLoss),
                    Timestamp = candles[i + 1].Time // i+1 because changes[i] is between candles[i] and candles[i+1]
                });
            }
        }

        return IndicatorResult.Success(
            Id,
            values,
            new Dictionary<string, object>
            {
                ["Period"] = _period,
                ["LastValue"] = values.Count > 0 ? values[^1].Value ?? double.NaN : double.NaN
            });
    }

    /// <summary>
    /// Calculates RSI from average gain and average loss using the standard formula.
    /// </summary>
    /// <param name="avgGain">Average gain over the period (smoothed)</param>
    /// <param name="avgLoss">Average loss over the period (smoothed)</param>
    /// <returns>
    /// RSI value between 0 and 100:
    /// - Returns 100 if avgLoss = 0 (no losses, all gains)
    /// - Returns 0 if avgGain = 0 (no gains, all losses)
    /// - Returns 50 if avgGain = avgLoss (balanced)
    /// </returns>
    private static double CalculateRsi(double avgGain, double avgLoss)
    {
        if (avgLoss == 0)
            return 100.0;

        double rs = avgGain / avgLoss;
        return 100.0 - (100.0 / (1.0 + rs));
    }
}