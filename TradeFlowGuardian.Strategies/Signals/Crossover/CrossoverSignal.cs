using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Signals.Base;

namespace TradeFlowGuardian.Strategies.Signals.Crossover;

/// <summary>
/// Generates trading signals when a fast indicator crosses above or below a slow indicator.
/// </summary>
/// <remarks>
/// <para>
/// 📚 What is a Crossover Signal?
/// </para>
/// <para>
/// A crossover signal is one of the most fundamental and widely-used trading signal types. It occurs when
/// two indicators (typically moving averages) cross each other, suggesting a potential change in trend direction.
/// Think of it as two lines racing - when the faster one overtakes the slower one, something important is happening.
/// </para>
/// <para>
/// Classic examples:
/// </para>
/// <list type="bullet">
///   <item><description>Golden Cross: 50-day SMA crosses above 200-day SMA (bullish, major trend change)</description></item>
///   <item><description>Death Cross: 50-day SMA crosses below 200-day SMA (bearish, major trend change)</description></item>
///   <item><description>MACD Signal Line Cross: MACD line crosses signal line (momentum change)</description></item>
///   <item><description>Fast/Slow MA Cross: 12 EMA crosses 26 EMA (short-term trend change)</description></item>
/// </list>
/// <para>
/// 🎯 Purpose and Use Cases:
/// </para>
/// <list type="bullet">
///   <item><description>Trend Reversal Detection: Identify when market shifts from uptrend to downtrend (or vice versa)</description></item>
///   <item><description>Entry Signals: Generate buy/sell signals when crossover confirms direction change</description></item>
///   <item><description>Momentum Confirmation: Fast indicator catching up to or surpassing slow one = building momentum</description></item>
///   <item><description>Exit Signals: Opposite crossover can signal when to close existing positions</description></item>
///   <item><description>Objective Entry/Exit: Removes emotion - clear, rules-based signal</description></item>
/// </list>
/// <para>
/// 🔢 How Crossover Detection Works:
/// </para>
/// <code>
/// Step 1: Get current and previous values for both indicators
/// FastPrevious, FastCurrent, SlowPrevious, SlowCurrent
/// 
/// Step 2: Check for bullish crossover (fast crosses above slow)
/// Condition: FastPrevious &lt;= SlowPrevious AND FastCurrent > SlowCurrent
/// 
/// Example:
/// Previous bar: Fast=1.0850, Slow=1.0855 (fast below slow)
/// Current bar:  Fast=1.0860, Slow=1.0857 (fast now above slow)
/// → BULLISH CROSSOVER detected! Generate BUY signal
/// 
/// Step 3: Check for bearish crossover (fast crosses below slow)
/// Condition: FastPrevious >= SlowPrevious AND FastCurrent &lt; SlowCurrent
/// 
/// Example:
/// Previous bar: Fast=1.0920, Slow=1.0915 (fast above slow)
/// Current bar:  Fast=1.0910, Slow=1.0913 (fast now below slow)
/// → BEARISH CROSSOVER detected! Generate SELL signal
/// 
/// Step 4: Calculate confidence based on separation
/// Confidence = (Separation / SlowValue) × 100
/// 
/// Why? Larger separation = stronger signal, less likely to be noise
/// 
/// Example:
/// Fast=1.0900, Slow=1.0880
/// Separation = 0.0020 (20 pips)
/// Confidence = (0.0020 / 1.0880) × 100 ≈ 0.18% → cap at 1.0 (100%)
/// 
/// Larger crossover angles = higher confidence = stronger conviction
/// </code>
/// <para>
/// 💡 Key Insight: We use &lt;= and >= for "previous" comparison (not just &lt; or >) to catch crossovers
/// that happen after periods when the indicators were touching or equal. This prevents missing valid signals.
/// </para>
/// <para>
/// ⚡ Types of Crossovers and Their Meaning:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Crossover Type</term>
///     <description>Indicators</description>
///     <description>Signal Meaning</description>
///     <description>Typical Use</description>
///   </listheader>
///   <item>
///     <term>Fast MA × Slow MA</term>
///     <description>12 EMA × 26 EMA</description>
///     <description>Short-term trend change</description>
///     <description>Day trading, quick entries</description>
///   </item>
///   <item>
///     <term>Golden/Death Cross</term>
///     <description>50 SMA × 200 SMA</description>
///     <description>Major trend reversal</description>
///     <description>Position trading, long-term bias</description>
///   </item>
///   <item>
///     <term>MACD Signal Cross</term>
///     <description>MACD × Signal Line</description>
///     <description>Momentum shift</description>
///     <description>Momentum trading, divergence plays</description>
///   </item>
///   <item>
///     <term>Price × MA</term>
///     <description>Price × 20 EMA</description>
///     <description>Trend continuation/rejection</description>
///     <description>Pullback entries, dynamic S/R</description>
///   </item>
///   <item>
///     <term>Oscillator × Zero</term>
///     <description>Stochastic × 20/80</description>
///     <description>Overbought/oversold exit</description>
///     <description>Mean reversion, range trading</description>
///   </item>
/// </list>
/// <para>
/// 🎬 Real-World Trading Scenarios:
/// </para>
/// <example>
/// <code>
/// // Example 1: Classic EMA crossover strategy (beginner)
/// // 12 EMA crosses 26 EMA - one of the most popular trading signals
/// var fastEma = new EmaIndicator("ema_12", 12);
/// var slowEma = new EmaIndicator("ema_26", 26);
/// 
/// var crossoverSignal = new CrossoverSignal(
///     id: "ema_cross_12_26",
///     fastIndicatorId: "ema_12",
///     slowIndicatorId: "ema_26"
/// );
/// 
/// // In your strategy:
/// var result = crossoverSignal.Generate(context);
/// 
/// if (result.Direction == SignalDirection.Long)
/// {
///     Console.WriteLine($"🎯 BUY Signal: 12 EMA crossed above 26 EMA");
///     Console.WriteLine($"   Confidence: {result.Confidence:P1}");
///     // Enter long position
/// }
/// else if (result.Direction == SignalDirection.Short)
/// {
///     Console.WriteLine($"💀 SELL Signal: 12 EMA crossed below 26 EMA");
///     Console.WriteLine($"   Confidence: {result.Confidence:P1}");
///     // Enter short position
/// }
/// 
/// // Example 2: Golden Cross detector (major trend changes)
/// var sma50 = new SmaIndicator("sma_50", 50);
/// var sma200 = new SmaIndicator("sma_200", 200);
/// 
/// var goldenCross = new CrossoverSignal(
///     id: "golden_cross",
///     fastIndicatorId: "sma_50",
///     slowIndicatorId: "sma_200"
/// );
/// 
/// var signal = goldenCross.Generate(context);
/// 
/// if (signal.Direction == SignalDirection.Long)
/// {
///     Console.WriteLine("🌟 GOLDEN CROSS! 50 SMA crossed above 200 SMA");
///     Console.WriteLine("   Major bullish signal - consider long-term long positions");
/// }
/// else if (signal.Direction == SignalDirection.Short)
/// {
///     Console.WriteLine("☠️ DEATH CROSS! 50 SMA crossed below 200 SMA");
///     Console.WriteLine("   Major bearish signal - consider long-term short positions");
/// }
/// 
/// // Example 3: Using with filters (recommended approach)
/// // Don't trade every crossover - filter for quality setups
/// var crossSignal = new CrossoverSignal("ema_cross", "ema_12", "ema_26");
/// 
/// var filters = new AndFilter(
///     "quality_crossover",
///     new AdxFilter("adx_25", 25m),           // Only when trending
///     new VolatilityFilter(0.95m),            // Only when market is moving
///     new TimeFilter("active_hours", 
///         TimeSpan.FromHours(8), 
///         TimeSpan.FromHours(16))              // Only during active hours
/// );
/// 
/// // Check filters first
/// var filterResult = filters.Evaluate(context);
/// if (filterResult.Passed)
/// {
///     var signal = crossSignal.Generate(context);
///     if (signal.Direction != SignalDirection.Neutral)
///     {
///         Console.WriteLine("✅ HIGH-QUALITY CROSSOVER: All filters passed + crossover detected");
///         // This is a premium setup - higher probability of success
///     }
/// }
/// 
/// // Example 4: Exit signal (opposite crossover)
/// // Use crossover to exit when trend reverses
/// bool inLongPosition = true; // You're holding a long trade
/// 
/// var exitSignal = crossoverSignal.Generate(context);
/// 
/// if (inLongPosition &amp;&amp; exitSignal.Direction == SignalDirection.Short)
/// {
///     Console.WriteLine("🚪 EXIT LONG: Bearish crossover detected while in long position");
///     Console.WriteLine("   Fast EMA crossed below Slow EMA - trend reversing");
///     // Close long position
/// }
/// 
/// // Example 5: Confidence-based position sizing
/// // Higher confidence crossovers = larger position size
/// var signal = crossoverSignal.Generate(context);
/// 
/// if (signal.Direction == SignalDirection.Long)
/// {
///     decimal basePositionSize = 1.0m; // 1 standard lot
///     
///     if (signal.Confidence > 0.8)
///     {
///         var positionSize = basePositionSize * 1.5m;
///         Console.WriteLine($"🔥 STRONG SIGNAL (confidence {signal.Confidence:P0})");
///         Console.WriteLine($"   Increasing position to {positionSize} lots");
///     }
///     else if (signal.Confidence > 0.5)
///     {
///         Console.WriteLine($"📊 MODERATE SIGNAL (confidence {signal.Confidence:P0})");
///         Console.WriteLine($"   Standard position: {basePositionSize} lot");
///     }
///     else
///     {
///         var positionSize = basePositionSize * 0.5m;
///         Console.WriteLine($"⚠️ WEAK SIGNAL (confidence {signal.Confidence:P0})");
///         Console.WriteLine($"   Reducing position to {positionSize} lots");
///     }
/// }
/// 
/// // Example 6: Debugging - understanding why no signal
/// var result = crossoverSignal.Generate(context);
/// 
/// if (result.Direction == SignalDirection.Neutral)
/// {
///     Console.WriteLine($"No crossover: {result.Reason}");
///     
///     if (result.Diagnostics != null)
///     {
///         Console.WriteLine("Diagnostics:");
///         foreach (var kvp in result.Diagnostics)
///         {
///             Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
///         }
///     }
/// }
/// </code>
/// </example>
/// <para>
/// ⚠️ Common Mistakes and Solutions:
/// </para>
/// <list type="bullet">
///   <item><description>❌ Mistake: Trading every crossover without filters
///     <para>Problem: In choppy/ranging markets, crossovers whipsaw constantly. 50%+ are false signals.</para>
///     <para>✅ Solution: Combine with ADX filter (only trade when ADX > 25), volatility filter, time filter.</para>
///   </description></item>
///   <item><description>❌ Mistake: Using very short periods (5/10) without confirmation
///     <para>Problem: Fast crossovers are noisy, generate many false signals in sideways markets.</para>
///     <para>✅ Solution: Use 12/26 or longer, or add 2-3 candle confirmation (wait for follow-through).</para>
///   </description></item>
///   <item><description>❌ Mistake: Not considering crossover angle
///     <para>Problem: Shallow crossovers (small separation) often fail - just noise touching.</para>
///     <para>✅ Solution: This signal includes confidence based on separation. Only trade confidence > 0.5.</para>
///   </description></item>
///   <item><description>❌ Mistake: Chasing crossovers that already moved
///     <para>Problem: By the time you see crossover, price may have already moved 50 pips.</para>
///     <para>✅ Solution: Set alert for approaching crossover (when fast within 5 pips of slow), prepare to enter.</para>
///   </description></item>
///   <item><description>❌ Mistake: Using same parameters for all instruments/timeframes
///     <para>Problem: 12/26 works for EUR/USD H1, might be terrible for GBP/JPY M15.</para>
///     <para>✅ Solution: Backtest and optimize periods for each instrument/timeframe combination.</para>
///   </description></item>
///   <item><description>❌ Mistake: Ignoring the broader trend
///     <para>Problem: Taking bearish crossover signal when price is in strong uptrend = fighting the trend.</para>
///     <para>✅ Solution: Add 200 SMA filter - only take bullish crosses above 200 SMA, bearish below.</para>
///   </description></item>
/// </list>
/// <para>
/// 🎓 Advanced Techniques:
/// </para>
/// <list type="bullet">
///   <item><description>Multiple Timeframe Confirmation: Require crossover on H1 AND H4 for stronger signal</description></item>
///   <item><description>Divergence Detection: When price makes new high but crossover doesn't confirm = warning sign</description></item>
///   <item><description>Volume Confirmation: Crossover with high volume = more reliable than low volume</description></item>
///   <item><description>Price Action Confluence: Best crossovers happen at key support/resistance levels</description></item>
///   <item><description>Momentum Confirmation: Combine with RSI - bullish cross when RSI > 50, bearish when RSI &lt; 50</description></item>
/// </list>
/// <para>
/// 📊 Expected Performance Characteristics:
/// </para>
/// <list type="bullet">
///   <item><description>Win Rate: 40-55% (many false signals in ranges, but winners can be large)</description></item>
///   <item><description>Profit Factor: 1.5-2.0 with good filtering, &lt;1.0 without filters</description></item>
///   <item><description>Trade Frequency: Depends on periods - 12/26 generates 2-5 signals per week, 50/200 few per year</description></item>
///   <item><description>Best in: Trending markets (ADX > 25), trending instruments, trending timeframes</description></item>
///   <item><description>Worst in: Ranging/choppy markets, during low volatility, around major S/R levels</description></item>
/// </list>
/// <para>
/// 🏆 Professional Tips:
/// </para>
/// <list type="number">
///   <item><description>Wait for candle close: Don't trade mid-candle crossovers - they often reverse by close</description></item>
///   <item><description>Require 2-3 candle confirmation: Reduces false signals by 30-40%, slightly delays entry</description></item>
///   <item><description>Scale in: Enter 50% position on crossover, add 50% if price confirms direction after 5-10 candles</description></item>
///   <item><description>Use ATR for stops: Set stop at entry ± 2×ATR, adapts to volatility</description></item>
///   <item><description>Trail with the slow MA: As trend continues, trail stop just below slow MA (for longs)</description></item>
///   <item><description>Exit on opposite crossover OR when price crosses slow MA: Whichever comes first</description></item>
/// </list>
/// <para>
/// 📈 Historical Context:
/// </para>
/// <para>
/// Moving average crossovers are one of the oldest technical trading strategies, used since the 1960s when
/// computers first made it practical to calculate moving averages on large datasets. The "Golden Cross" and
/// "Death Cross" terms became popular in the 1970s-1980s and are still watched by institutional traders today.
/// </para>
/// <para>
/// The popularity of 12/26 EMA crossovers comes from the MACD indicator (which uses these exact periods),
/// created by Gerald Appel in the 1970s. The 50/200 SMA crossover is watched globally - when it happens
/// on major indices (S&amp;P 500, Dow Jones), it makes financial news headlines because institutional algorithms
/// monitor and react to it, creating self-fulfilling prophecy effects.
/// </para>
/// </remarks>
public sealed class CrossoverSignal : SignalBase
{
    private readonly string _fastIndicatorId;
    private readonly string _slowIndicatorId;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrossoverSignal"/> class.
    /// </summary>
    /// <param name="id">
    /// Unique identifier for this signal instance (e.g., "ema_cross_12_26", "golden_cross").
    /// Used for diagnostics and signal composition in strategies.
    /// </param>
    /// <param name="fastIndicatorId">
    /// ID of the faster-moving indicator (shorter period) that will cross the slower indicator.
    /// <para>Examples: "ema_12", "sma_50", "macd_line"</para>
    /// <para>Must match the ID used when registering the indicator in the market context.</para>
    /// </param>
    /// <param name="slowIndicatorId">
    /// ID of the slower-moving indicator (longer period) that the faster indicator crosses.
    /// <para>Examples: "ema_26", "sma_200", "macd_signal"</para>
    /// <para>Must match the ID used when registering the indicator in the market context.</para>
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when fastIndicatorId or slowIndicatorId is null.
    /// </exception>
    public CrossoverSignal(string id, string fastIndicatorId, string slowIndicatorId)
        : base(id, "Crossover")
    {
        _fastIndicatorId = fastIndicatorId ?? throw new ArgumentNullException(nameof(fastIndicatorId));
        _slowIndicatorId = slowIndicatorId ?? throw new ArgumentNullException(nameof(slowIndicatorId));
    }

    /// <summary>
    /// Generates a trading signal by detecting crossovers between fast and slow indicators.
    /// </summary>
    /// <param name="context">
    /// Market context containing computed indicator results.
    /// Must include both fastIndicatorId and slowIndicatorId with valid, computed values.
    /// </param>
    /// <returns>
    /// <see cref="SignalResult"/> containing:
    /// <list type="bullet">
    ///   <item><description>Direction: Long (bullish crossover), Short (bearish crossover), or Neutral (no crossover)</description></item>
    ///   <item><description>Confidence: 0.0-1.0 based on separation between indicators (larger = more confident)</description></item>
    ///   <item><description>Reason: Human-readable explanation of the signal</description></item>
    ///   <item><description>Diagnostics: Fast/slow current values, separation distance</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// Detection Logic:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Bullish Crossover: FastPrev &lt;= SlowPrev AND FastCurr > SlowCurr</description></item>
    ///   <item><description>Bearish Crossover: FastPrev >= SlowPrev AND FastCurr &lt; SlowCurr</description></item>
    ///   <item><description>No Crossover: Neither condition met (parallel lines or no change)</description></item>
    /// </list>
    /// <para>
    /// Confidence Calculation:
    /// </para>
    /// <code>
    /// Separation = |FastValue - SlowValue|
    /// Confidence = min(1.0, (Separation / SlowValue) × 100)
    /// 
    /// Why this formula?
    /// - Relative to slow value (percentage-based, not absolute)
    /// - Larger separation = stronger signal = higher confidence
    /// - Capped at 1.0 (100% confidence) to prevent unrealistic values
    /// - Small separations (touching lines) = low confidence = weak signal
    /// </code>
    /// <para>
    /// Error Handling:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Indicators not found in context: Returns Neutral with "Indicators not found" reason</description></item>
    ///   <item><description>Indicators invalid: Returns Neutral with "Indicators invalid" reason</description></item>
    ///   <item><description>Insufficient history (&lt;2 values): Returns Neutral with "Insufficient history" reason</description></item>
    ///   <item><description>Missing values (nulls): Returns Neutral with "Missing values" reason</description></item>
    /// </list>
    /// <para>
    /// Performance: O(1) - just checks last 2 values from pre-computed indicators. Execution time &lt;0.1ms.
    /// </para>
    /// </remarks>
    protected override SignalResult GenerateCore(IMarketContext context)
    {
        // Try to get both indicators from the context
        if (!context.Indicators.TryGetValue(_fastIndicatorId, out var fastResult) ||
            !context.Indicators.TryGetValue(_slowIndicatorId, out var slowResult))
        {
            return NeutralResult(
                $"Indicators not found: {_fastIndicatorId} or {_slowIndicatorId} missing from context",
                context.TimestampUtc);
        }

        // Check if indicator computations were successful
        if (!fastResult.IsValid || !slowResult.IsValid)
        {
            return NeutralResult(
                $"Indicators invalid: Fast={fastResult.ErrorReason}, Slow={slowResult.ErrorReason}",
                context.TimestampUtc);
        }

        // Get the indicator value lists (IReadOnlyList<IndicatorValue>)
        var fastValues = fastResult.Values;
        var slowValues = slowResult.Values;

        // Need at least 2 values (current and previous) to detect crossover
        if (fastValues.Count < 2 || slowValues.Count < 2)
        {
            return NeutralResult(
                $"Insufficient history: Fast has {fastValues.Count}, Slow has {slowValues.Count}, need at least 2 each",
                context.TimestampUtc);
        }

        // Get current (most recent) and previous values
        var fastCurrent = fastValues[^1]; // Last element
        var fastPrevious = fastValues[^2]; // Second to last
        var slowCurrent = slowValues[^1];
        var slowPrevious = slowValues[^2];

        // Check for null values (can happen during warm-up period)
        if (!fastCurrent.Value.HasValue || !fastPrevious.Value.HasValue ||
            !slowCurrent.Value.HasValue || !slowPrevious.Value.HasValue)
        {
            return NeutralResult(
                "Missing values: One or more indicator values are null (still in warm-up period)",
                context.TimestampUtc);
        }

        // Extract the actual numeric values
        double fastCurr = fastCurrent.Value.Value;
        double fastPrev = fastPrevious.Value.Value;
        double slowCurr = slowCurrent.Value.Value;
        double slowPrev = slowPrevious.Value.Value;

        // Detect crossovers
        // Bullish: Fast was at or below slow, now above (upward crossover)
        bool bullishCross = fastPrev <= slowPrev && fastCurr > slowCurr;

        // Bearish: Fast was at or above slow, now below (downward crossover)
        bool bearishCross = fastPrev >= slowPrev && fastCurr < slowCurr;

        if (bullishCross)
        {
            // Calculate separation (how far apart the lines are after crossing)
            double separation = fastCurr - slowCurr;

            // Confidence based on relative separation
            // Larger separation = more significant crossover = higher confidence
            double confidenceRaw = Math.Abs(separation) / slowCurr * 100.0;
            double confidence = Math.Min(1.0, confidenceRaw);

            return new SignalResult
            {
                Direction = SignalDirection.Long,
                Confidence = confidence,
                Reason = $"Bullish crossover: {_fastIndicatorId} crossed above {_slowIndicatorId}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["FastCurrent"] = fastCurr,
                    ["FastPrevious"] = fastPrev,
                    ["SlowCurrent"] = slowCurr,
                    ["SlowPrevious"] = slowPrev,
                    ["Separation"] = separation,
                    ["ConfidenceRaw"] = confidenceRaw,
                    ["FastTimestamp"] = fastCurrent.Timestamp,
                    ["SlowTimestamp"] = slowCurrent.Timestamp
                }
            };
        }

        if (bearishCross)
        {
            // Calculate separation
            double separation = slowCurr - fastCurr;

            // Confidence based on relative separation
            double confidenceRaw = Math.Abs(separation) / slowCurr * 100.0;
            double confidence = Math.Min(1.0, confidenceRaw);

            return new SignalResult
            {
                Direction = SignalDirection.Short,
                Confidence = confidence,
                Reason = $"Bearish crossover: {_fastIndicatorId} crossed below {_slowIndicatorId}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["FastCurrent"] = fastCurr,
                    ["FastPrevious"] = fastPrev,
                    ["SlowCurrent"] = slowCurr,
                    ["SlowPrevious"] = slowPrev,
                    ["Separation"] = separation,
                    ["ConfidenceRaw"] = confidenceRaw,
                    ["FastTimestamp"] = fastCurrent.Timestamp,
                    ["SlowTimestamp"] = slowCurrent.Timestamp
                }
            };
        }


        // No crossover detected
        return NeutralResult(
            $"No crossover detected (Fast: {fastCurr:F5} {(fastCurr > slowCurr ? "above" : "below")} Slow: {slowCurr:F5})",
            context.TimestampUtc
        );
    }
}