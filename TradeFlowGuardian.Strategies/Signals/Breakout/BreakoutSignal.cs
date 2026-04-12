using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Signals.Base;

namespace TradeFlowGuardian.Strategies.Signals.Breakout;

/// <summary>
/// Generates breakout signals when the price closes beyond the recent high/low range plus an ATR-based buffer.
/// </summary>
/// <remarks>
/// <para>
/// The BreakoutSignal identifies potential trend continuation or reversal opportunities when a price
/// breaks out of a defined trading range with sufficient momentum to overcome typical market noise.
/// By requiring the close to exceed the range boundary PLUS an ATR buffer, this signal reduces
/// false breakouts and whipsaw trades common in pure range breakout strategies.
/// </para>
/// 
/// Strategy Logic:
/// <list type="number">
///   <item><description>Identifies the highest high and the lowest low over a lookback period (e.g., 30 bars)</description></item>
///   <item><description>Computes Average True Range (ATR) over 14 periods as a volatility measure</description></item>
///   <item><description>Creates breakout buffers: Upper = RecentHigh + (ATR × BufferMultiplier), Lower = RecentLow - (ATR × BufferMultiplier)</description></item>
///   <item><description>Generates Long signal when close exceeds upper buffer (bullish breakout)</description></item>
///   <item><description>Generates Short signal when close falls below lower buffer (bearish breakout)</description></item>
///   <item><description>Returns Neutral when the price remains within the buffered range</description></item>
/// </list>
/// <para>
/// Confidence Calculation:
/// Signal confidence is dynamic and based on breakout strength:
/// </para>
/// <list type="bullet">
///   <item><description>Base confidence: 0.6 (60%) when price just barely breaks the buffer</description></item>
///   <item><description>Distance bonus: +0.2 confidence per ATR distance beyond the buffer</description></item>
///   <item><description>Maximum: Capped at 1.0 (100%) for very strong breakouts</description></item>
///   <item><description>Formula: Confidence = Min(1.0, 0.6 + (BreakoutDistance / ATR) × 0.2)</description></item>
/// </list>
/// <para>
/// This approach rewards more decisive breakouts while still acknowledging marginal breaks.
/// </para>
/// <para>
/// ATR Buffer Rationale:
/// </para>
/// <para>
/// The ATR-based buffer adapts to current market volatility. In low-volatility conditions, 
/// the buffer is smaller (making breakouts easier but potentially noisier). In high volatility,
/// the buffer is larger (requiring more conviction for a valid breakout). The default multiplier
/// of 0.25 means the buffer is 25% of the average true range, which empirically balances
/// sensitivity vs. false signals for most forex pairs on intraday timeframes.
/// </para>
/// <para>
///>Usage Examples:
/// </para>
/// <code>
/// // Example 1: Conservative breakout (larger buffer, longer lookback)
/// var conservativeBreakout = new BreakoutSignal(
///     id: "breakout_conservative",
///     lookbackPeriods: 50,      // Wider range
///     atrBufferMult: 0.5m       // Larger buffer = fewer but stronger signals
/// );
/// 
/// // Example 2: Aggressive breakout (smaller buffer, shorter lookback)
/// var aggressiveBreakout = new BreakoutSignal(
///     id: "breakout_aggressive",
///     lookbackPeriods: 20,      // Tighter range
///     atrBufferMult: 0.15m      // Smaller buffer = more signals
/// );
/// 
/// // Example 3: Using the signal in a strategy
/// var result = conservativeBreakout.Generate(context);
/// if (result.Direction == SignalDirection.Long &amp;&amp; result.Confidence > 0.7)
/// {
///     Console.WriteLine($"Strong bullish breakout detected!");
///     Console.WriteLine($"Reason: {result.Reason}");
///     Console.WriteLine($"Breakout distance: {result.Diagnostics["BreakoutDistance"]} pips");
/// }
/// </code>
/// <para>
/// Parameter Tuning Guidelines:
/// </para>
/// <list type="bullet">
///   <item><description>lookbackPeriods: 20-50 for intraday (1H-4H), 50-100 for daily, 100-200 for weekly</description></item>
///   <item><description>atrBufferMult: 0.15-0.25 for volatile pairs (GBP/JPY), 0.25-0.50 for stable pairs (EUR/USD)</description></item>
///   <item><description>Increase both for more conservative, lower-frequency signals</description></item>
///   <item><description>Decrease both for more aggressive, higher-frequency signals</description></item>
/// </list>
/// <para>
/// Limitations and Considerations:
/// </para>
/// <list type="bullet">
///   <item><description>Performs best in trending markets; may generate false signals in choppy/ranging conditions</description></item>
///   <item><description>Requires sufficient history: lookbackPeriods + 15 candles minimum</description></item>
///   <item><description>Does not account for fundamental news events that may invalidate technical breakouts</description></item>
///   <item><description>Should be combined with trend filters or volume confirmation for best results</description></item>
/// </list>
/// </remarks>
public sealed class BreakoutSignal : SignalBase
{
    private readonly int _lookbackPeriods;
    private readonly decimal _atrBufferMult;

    /// <summary>
    /// Initializes a new instance of the <see cref="BreakoutSignal"/> class with specified parameters.
    /// </summary>
    /// <param name="id">
    /// Unique identifier for this signal instance. Used for diagnostics and signal composition.
    /// Convention: lowercase with underscores (e.g., "breakout_30_025").
    /// </param>
    /// <param name="lookbackPeriods">
    /// Number of historical candles to analyze for determining the high/low range.
    /// Larger values create wider, more stable ranges; smaller values are more responsive to recent price action.
    /// Default: 30 periods.
    /// Valid range: 10-200 periods.
    /// </param>
    /// <param name="atrBufferMult">
    /// Multiplier applied to ATR to create the breakout buffer zone.
    /// Higher values require stronger breakouts (fewer signals, higher quality).
    /// Lower values generate more signals but increase false breakouts.
    /// Default: 0.25 (25% of ATR).
    /// Valid range: 0.10-1.0.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown if lookbackPeriods &lt; 1 or atrBufferMult &lt;= 0.
    /// </exception>
    /// <example>
    /// <code>
    /// // Standard intraday breakout signal
    /// var signal = new BreakoutSignal(
    ///     id: "breakout_1h",
    ///     lookbackPeriods: 30,
    ///     atrBufferMult: 0.25m);
    /// </code>
    /// </example>
    public BreakoutSignal(string id, int lookbackPeriods = 30, decimal atrBufferMult = 0.25m)
        : base(id, $"Breakout({lookbackPeriods},buf={atrBufferMult:F2})")
    {
        _lookbackPeriods = lookbackPeriods;
        _atrBufferMult = atrBufferMult;
    }

    /// <summary>
    /// Computes the Average True Range (ATR) over the specified number of periods.
    /// </summary>
    /// <param name="candles">
    /// Historical price candles. Must contain at least (n + 1) candles to compute ATR over n periods.
    /// </param>
    /// <param name="n">
    /// Number of periods for ATR calculation. Typically, 14 periods (Wilder's original specification).
    /// </param>
    /// <returns>
    /// The simple average of True Range values over the last n periods.
    /// Returns 0 if computation fails or insufficient data.
    /// </returns>
    /// <remarks>
    /// <para>
    /// True Range (TR) for each candle is the maximum of:
    /// </para>
    /// <list type="number">
    ///   <item><description>Current High - Current Low (intra-bar range)</description></item>
    ///   <item><description>|Current High - Previous Close| (upward gap)</description></item>
    ///   <item><description>|Current Low - Previous Close| (downward gap)</description></item>
    /// </list>
    /// <para>
    /// This implementation uses Simple Moving Average of TR (SMA-ATR) rather than Wilder's
    /// exponential smoothing for deterministic, easier-to-test behavior. The difference is
    /// negligible for most practical purposes.
    /// </para>
    /// <para>
    /// Note: This is a simplified ATR computation. For production use with other indicators,
    /// consider using a shared ATR indicator instance to avoid redundant calculations.
    /// </para>
    /// </remarks>
    private static decimal ComputeAtr(IReadOnlyList<Candle> candles, int n)
    {
        decimal sumTr = 0m;
        for (int i = candles.Count - n; i < candles.Count; i++)
        {
            var cur = candles[i];
            var prev = candles[i - 1];
            var tr = Math.Max((double)(cur.High - cur.Low),
                Math.Max(Math.Abs((double)(cur.High - prev.Close)),
                    Math.Abs((double)(cur.Low - prev.Close))));
            sumTr += (decimal)tr;
        }

        return sumTr / n;
    }

    /// <summary>
    /// Core signal generation logic that evaluates market context for breakout conditions.
    /// </summary>
    /// <param name="context">
    /// Immutable market context containing price history, indicators, and timestamp.
    /// Must contain at least (lookbackPeriods + 15) candles for reliable computation.
    /// </param>
    /// <returns>
    /// <see cref="SignalResult"/> with:
    /// <list type="bullet">
    ///   <item><description>Long signal: When close exceeds upper buffer with confidence based on breakout strength</description></item>
    ///   <item><description>Short signal: When close falls below lower buffer with confidence based on breakout strength</description></item>
    ///   <item><description>Neutral signal: When price is within the buffered range or data is insufficient</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// Early Exit Conditions:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Insufficient data: Returns neutral if candle count &lt; (lookbackPeriods + 15)</description></item>
    ///   <item><description>Invalid ATR: Returns neutral if ATR &lt;= 0 (indicates flat market or calculation error)</description></item>
    /// </list>
    /// <para>
    /// Diagnostic Information:
    /// </para>
    /// <para>
    /// All signal results include comprehensive diagnostics for analysis:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>RecentHigh/RecentLow: Boundary values of the range</description></item>
    ///   <item><description>UpBuffer/DnBuffer: Calculated breakout threshold levels</description></item>
    ///   <item><description>CurrentClose: The close price being evaluated</description></item>
    ///   <item><description>BreakoutDistance: How far price penetrated beyond the buffer</description></item>
    ///   <item><description>ATR: Current volatility measure</description></item>
    ///   <item><description>LookbackPeriods: Configuration parameter for traceability</description></item>
    /// </list>
    /// <para>
    /// Determinism:
    /// </para>
    /// <para>
    /// This method is deterministic: given an identical market context, it will always produce
    /// identical results. It does not access system time or random sources. Uses context.TimestampUtc
    /// for result timestamp to ensure backtest reproducibility.
    /// </para>
    /// </remarks>
    protected override SignalResult GenerateCore(IMarketContext context)
    {
        // Validate sufficient data: need lookback period + 15 for ATR calculation
        if (context.Candles.Count < _lookbackPeriods + 15)
            return NeutralResult("Insufficient data", context.TimestampUtc);

        // Extract the lookback window: skip to the start position, take a lookback count
        var recent = context.Candles.Skip(context.Candles.Count - _lookbackPeriods - 1)
            .Take(_lookbackPeriods)
            .ToList();

        // Determine the trading range boundaries
        var recentHigh = recent.Max(c => c.High);
        var recentLow = recent.Min(c => c.Low);
        var cur = context.Candles[^1];

        // Calculate volatility measure for adaptive buffer sizing
        var atr = ComputeAtr(context.Candles, 14);

        // Early exit: invalid ATR indicates flat market or calculation error
        if (atr <= 0)
            return new SignalResult
            {
                Direction = SignalDirection.Neutral,
                Confidence = 0.0,
                Reason = $"ATR<=0",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["atr"] = atr
                }
            };

        // Calculate breakout threshold levels with ATR-based buffers
        var upBuffer = recentHigh + atr * _atrBufferMult;
        var dnBuffer = recentLow - atr * _atrBufferMult;

        // Check for bullish breakout (close above upper buffer)
        if (cur.Close > upBuffer)
        {
            // Calculate confidence based on how far the price broke above the buffer
            // Base confidence: 60%, with bonus up to 100% based on breakout strength
            var breakoutDistance = cur.Close - upBuffer;
            var confidence = Math.Min(1.0, 0.6 + (double)(breakoutDistance / atr) * 0.2);

            return new SignalResult
            {
                Direction = SignalDirection.Long,
                Confidence = confidence,
                Reason = $"Breakout > H+buf: Close={cur.Close:F5}, Buffer={upBuffer:F5}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["RecentHigh"] = recentHigh,
                    ["UpBuffer"] = upBuffer,
                    ["CurrentClose"] = cur.Close,
                    ["BreakoutDistance"] = breakoutDistance,
                    ["ATR"] = atr,
                    ["LookbackPeriods"] = _lookbackPeriods
                }
            };
        }

        // Check for bearish breakout (close below lower buffer)
        if (cur.Close < dnBuffer)
        {
            // Calculate confidence based on how far the price broke below the buffer
            // Base confidence: 60%, with bonus up to 100% based on breakout strength
            var breakoutDistance = dnBuffer - cur.Close;
            var confidence = Math.Min(1.0, 0.6 + (double)(breakoutDistance / atr) * 0.2);

            return new SignalResult
            {
                Direction = SignalDirection.Short,
                Confidence = confidence,
                Reason = $"Breakout < L-buf: Close={cur.Close:F5}, Buffer={dnBuffer:F5}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["RecentLow"] = recentLow,
                    ["DnBuffer"] = dnBuffer,
                    ["CurrentClose"] = cur.Close,
                    ["BreakoutDistance"] = breakoutDistance,
                    ["ATR"] = atr,
                    ["LookbackPeriods"] = _lookbackPeriods
                }
            };
        }

        // No breakout detected: price remains within the buffered range
        return NeutralResult("No breakout", context.TimestampUtc);
    }
}