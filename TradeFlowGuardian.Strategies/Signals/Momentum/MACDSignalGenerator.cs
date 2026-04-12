using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Signals.Base;

namespace TradeFlowGuardian.Strategies.Signals.Momentum;

/// <summary>
/// Generates signals based on Moving Average Convergence Divergence (MACD) indicator crossovers and momentum.
/// </summary>
/// <remarks>
/// <para>
/// The MACDSignal is a momentum oscillator that identifies trend direction, momentum strength, and potential
/// reversals by measuring the relationship between two exponential moving averages. Unlike simple moving average
/// crossovers, MACD provides additional information through its signal line and histogram, enabling detection
/// of momentum shifts before price changes become obvious. MACD is one of the most widely used technical
/// indicators across all financial markets.
/// </para>
/// <para>
/// MACD Components:
/// </para>
/// <list type="bullet">
///   <item><description>MACD Line: Difference between fast EMA and slow EMA (typically 12-26)</description></item>
///   <item><description>Signal Line: EMA of the MACD line (typically 9 periods)</description></item>
///   <item><description>Histogram: Difference between MACD line and signal line (visual representation of momentum)</description></item>
/// </list>
/// <para>
/// Strategy Logic:
/// </para>
/// <list type="number">
///   <item><description>Computes fast EMA (default: 12 periods) on closing prices</description></item>
///   <item><description>Computes slow EMA (default: 26 periods) on closing prices</description></item>
///   <item><description>Calculates MACD line: Fast EMA - Slow EMA</description></item>
///   <item><description>Computes signal line: EMA(9) of MACD line</description></item>
///   <item><description>Calculates histogram: MACD line - Signal line</description></item>
///   <item><description>Primary signal: Histogram crosses zero line (momentum shift)</description></item>
///   <item><description>Secondary signal: Histogram expanding in direction (momentum strengthening)</description></item>
/// </list>
/// <para>
/// Signal Types Generated:
/// </para>
/// <list type="bullet">
///   <item><description>Histogram Cross Up: Histogram crosses from negative to positive (bullish momentum shift)</description></item>
///   <item><description>Histogram Cross Down: Histogram crosses from positive to negative (bearish momentum shift)</description></item>
///   <item><description>MACD Rising: Histogram expanding positively with MACD above signal (strengthening bull momentum)</description></item>
///   <item><description>MACD Falling: Histogram expanding negatively with MACD below signal (strengthening bear momentum)</description></item>
/// </list>
/// <para>
/// Confidence Calculation:
/// </para>
/// <para>
/// Signal confidence is based on histogram magnitude (momentum strength):
/// </para>
/// <list type="bullet">
///   <item><description>Base confidence: 0.5 (50%) for any valid crossover or momentum shift</description></item>
///   <item><description>Magnitude bonus: Up to +0.5 based on absolute histogram value normalized by recent range</description></item>
///   <item><description>Formula: Confidence = Min(1.0, 0.5 + (|Histogram| / HistogramRange) × 0.5)</description></item>
///   <item><description>Rationale: Larger histogram = stronger momentum divergence = higher conviction</description></item>
/// </list>
/// <para>
/// MACD Interpretation:
/// </para>
/// <list type="bullet">
///   <item><description>MACD &gt; 0: Fast EMA above slow EMA = bullish trend context</description></item>
///   <item><description>MACD &lt; 0: Fast EMA below slow EMA = bearish trend context</description></item>
///   <item><description>Histogram &gt; 0: MACD rising faster than signal = increasing bullish momentum</description></item>
///   <item><description>Histogram &lt; 0: MACD falling faster than signal = increasing bearish momentum</description></item>
///   <item><description>Histogram expanding: Momentum accelerating (trend strengthening)</description></item>
///   <item><description>Histogram contracting: Momentum decelerating (potential reversal warning)</description></item>
/// </list>
/// <para>
/// Standard Parameter Combinations:
/// </para>
/// <list type="bullet">
///   <item><description>Default (12, 26, 9): Gerald Appel's original parameters, most widely used</description></item>
///   <item><description>Fast (5, 13, 5): More responsive for day trading, more whipsaws</description></item>
///   <item><description>Slow (19, 39, 9): Smoother signals for swing trading, less sensitive</description></item>
///   <item><description>Conservative (24, 52, 9): Very smooth, weekly/monthly timeframes</description></item>
/// </list>
/// <para>
/// Usage Examples:
/// </para>
/// <code>
/// // Example 1: Standard MACD with default parameters
/// var standard = new MACDSignal(
///     id: "macd_standard",
///     fastPeriods: 12,
///     slowPeriods: 26,
///     signalPeriods: 9
/// );
/// 
/// // Example 2: Fast MACD for day trading
/// var dayTrading = new MACDSignal(
///     id: "macd_fast",
///     fastPeriods: 5,
///     slowPeriods: 13,
///     signalPeriods: 5
/// );
/// 
/// // Example 3: Conservative MACD for position trading
/// var conservative = new MACDSignal(
///     id: "macd_conservative",
///     fastPeriods: 24,
///     slowPeriods: 52,
///     signalPeriods: 9
/// );
/// 
/// // Example 4: Using the signal with histogram analysis
/// var result = standard.Generate(context);
/// if (result.Direction == SignalDirection.Long &amp;&amp; result.Confidence > 0.70)
/// {
///     var histogram = (decimal)result.Diagnostics["HistogramCurrent"];
///     var macdLine = (decimal)result.Diagnostics["MACDCurrent"];
///     
///     Console.WriteLine($"Strong bullish momentum detected!");
///     Console.WriteLine($"Histogram: {histogram:F5} (momentum strength)");
///     Console.WriteLine($"MACD Line: {macdLine:F5} (trend context)");
///     
///     if (macdLine > 0)
///         Console.WriteLine("Buying into established uptrend");
///     else
///         Console.WriteLine("Early reversal signal - use caution");
/// }
/// </code>
/// <para>
/// Parameter Tuning Guidelines:
/// </para>
/// <list type="bullet">
///   <item><description>Fast periods: 5-12 for responsive, 12-20 for standard, 20-30 for smooth</description></item>
///   <item><description>Slow periods: Typically 2-2.5× fast periods for clear separation</description></item>
///   <item><description>Signal periods: 5-9 for responsive, 9-12 for smooth, rarely change from 9</description></item>
///   <item><description>Timeframe scaling: Use faster parameters on higher timeframes (Daily+), slower on lower (1H-)</description></item>
/// </list>
/// <para>
/// Optimal Market Conditions:
/// </para>
/// <list type="bullet">
///   <item><description>Best: Trending markets with clear momentum shifts</description></item>
///   <item><description>Good: Markets transitioning from consolidation to trend</description></item>
///   <item><description>Acceptable: Ranging markets on higher timeframes (use histogram for reversals)</description></item>
///   <item><description>Poor: Tight choppy ranges (generates many false crossovers)</description></item>
///   <item><description>Avoid: Extremely low volatility, gapping markets</description></item>
/// </list>
/// <para>
/// MACD Divergences (Not Implemented Here):
/// </para>
/// <para>
/// MACD is particularly powerful for divergence detection (requires price comparison):
/// </para>
/// <list type="bullet">
///   <item><description>Bullish divergence: Price makes lower low, MACD makes higher low = reversal likely</description></item>
///   <item><description>Bearish divergence: Price makes higher high, MACD makes lower high = reversal likely</description></item>
///   <item><description>Hidden bullish: Price makes higher low, MACD makes lower low = continuation</description></item>
///   <item><description>Hidden bearish: Price makes lower high, MACD makes higher high = continuation</description></item>
/// </list>
/// <para>
/// Divergence detection requires swing point identification and is typically implemented as a separate signal.
/// </para>
/// <para>
/// Advantages Over Simple Crossovers:
/// </para>
/// <list type="bullet">
///   <item><description>Momentum information: Histogram shows acceleration/deceleration</description></item>
///   <item><description>Earlier signals: Histogram crosses before price typically reverses</description></item>
///   <item><description>Divergence detection: Can identify weakening trends before reversal</description></item>
///   <item><description>Trend context: MACD line position (above/below zero) shows overall trend</description></item>
///   <item><description>Multiple signals: Crossovers + momentum + divergences in one indicator</description></item>
/// </list>
/// <para>
/// Limitations and Considerations:
/// </para>
/// <list type="bullet">
///   <item><description>Lagging indicator: Based on EMAs, so inherently lagging price</description></item>
///   <item><description>Whipsaw prone: Generates false signals in ranging markets</description></item>
///   <item><description>No magnitude: Doesn't indicate how far price will move</description></item>
///   <item><description>No stop loss: Doesn't provide risk management levels</description></item>
///   <item><description>False divergences: Not all divergences lead to reversals</description></item>
///   <item><description>Complex interpretation: Multiple components require experience to read correctly</description></item>
/// </list>
/// <para>
/// Complementary Signals and Filters:
/// </para>
/// <para>
/// MACD signals are more effective when combined with:
/// </para>
/// <list type="bullet">
///   <item><description>TrendFilter: Only take MACD signals in direction of higher TF trend</description></item>
///   <item><description>Support/Resistance: MACD crossovers at key levels are higher probability</description></item>
///   <item><description>Volume: Confirm crossovers with increasing volume</description></item>
///   <item><description>Price action: Wait for price confirmation (higher high/lower low) after crossover</description></item>
///   <item><description>RSI: Combine with RSI for overbought/oversold context</description></item>
///   <item><description>Breakout signals: MACD + range breakout = high-conviction setup</description></item>
/// </list>
/// <para>
/// Historical Context:
/// </para>
/// <para>
/// MACD was developed by Gerald Appel in the late 1970s and has become one of the most popular
/// technical indicators. The standard (12, 26, 9) parameters were optimized for weekly charts
/// in the 1970s stock market. While these parameters remain standard, many modern traders adjust
/// them for different timeframes and market conditions. MACD is included in virtually every
/// charting platform and is taught in most technical analysis courses.
/// </para>
/// </remarks>
public sealed class MacdSignal : SignalBase
{
    private readonly int _fastPeriods;
    private readonly int _slowPeriods;
    private readonly int _signalPeriods;

    /// <summary>
    /// Initializes a new instance of the <see cref="MacdSignal"/> class with specified parameters.
    /// </summary>
    /// <param name="id">
    /// Unique identifier for this signal instance. Used for diagnostics and signal composition.
    /// Convention: lowercase with underscores (e.g., "macd_standard", "macd_fast").
    /// </param>
    /// <param name="fastPeriods">
    /// Number of periods for the fast EMA component of MACD line.
    /// Default: 12 periods (Appel's original parameter).
    /// Valid range: 5-30 periods.
    /// </param>
    /// <param name="slowPeriods">
    /// Number of periods for the slow EMA component of MACD line.
    /// Should be 2-2.5× larger than fast periods.
    /// Default: 26 periods (Appel's original parameter).
    /// Valid range: 10-60 periods.
    /// </param>
    /// <param name="signalPeriods">
    /// Number of periods for the signal line (EMA of MACD line).
    /// Default: 9 periods (Appel's original parameter).
    /// Valid range: 5-15 periods.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if id is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown if fastPeriods &lt; 5, slowPeriods &lt;= fastPeriods, signalPeriods &lt; 3, or parameters exceed max ranges.
    /// </exception>
    public MacdSignal(string id, int fastPeriods = 12, int slowPeriods = 26, int signalPeriods = 9)
        : base(id, $"MACD({fastPeriods},{slowPeriods},{signalPeriods})")
    {
        if (fastPeriods < 5 || fastPeriods > 30)
            throw new ArgumentException("Fast periods must be between 5 and 30", nameof(fastPeriods));
        if (slowPeriods <= fastPeriods || slowPeriods > 60)
            throw new ArgumentException("Slow periods must be > fast periods and <= 60", nameof(slowPeriods));
        if (signalPeriods < 3 || signalPeriods > 15)
            throw new ArgumentException("Signal periods must be between 3 and 15", nameof(signalPeriods));

        _fastPeriods = fastPeriods;
        _slowPeriods = slowPeriods;
        _signalPeriods = signalPeriods;
    }

    /// <summary>
    /// Computes Exponential Moving Average (EMA) sequence for a series of values.
    /// </summary>
    /// <param name="values">Series of values to compute EMA over.</param>
    /// <param name="period">Number of periods for EMA calculation.</param>
    /// <returns>List of EMA values, one per input value.</returns>
    private static List<decimal> ComputeEmaSequence(IReadOnlyList<decimal> values, int period)
    {
        var result = new List<decimal>(values.Count);
        if (values.Count < period) return result;

        // Seed with SMA
        decimal ema = values.Take(period).Average();
        result.Add(ema);

        decimal k = 2m / (period + 1);

        for (int i = period; i < values.Count; i++)
        {
            ema = values[i] * k + ema * (1 - k);
            result.Add(ema);
        }

        return result;
    }

    protected override SignalResult GenerateCore(IMarketContext context)
    {
        // Need enough data for slow EMA + signal EMA + 2 bars for comparison
        var minCandles = _slowPeriods + _signalPeriods + 2;

        if (context.Candles.Count < minCandles)
            return NeutralResult(
                $"Insufficient data: need {minCandles}, have {context.Candles.Count}",
                context.TimestampUtc);

        // Extract closing prices
        var closes = context.Candles.Select(c => c.Close).ToList();

        // Compute fast and slow EMAs
        var fastEmaSeq = ComputeEmaSequence(closes, _fastPeriods);
        var slowEmaSeq = ComputeEmaSequence(closes, _slowPeriods);

        if (fastEmaSeq.Count < 2 || slowEmaSeq.Count < 2)
            return NeutralResult("EMA sequences too short", context.TimestampUtc);

        // Compute MACD line (difference between fast and slow EMAs)
        var macdSeq = new List<decimal>(fastEmaSeq.Count);
        for (int i = 0; i < Math.Min(fastEmaSeq.Count, slowEmaSeq.Count); i++)
        {
            macdSeq.Add(fastEmaSeq[i] - slowEmaSeq[i]);
        }

        if (macdSeq.Count < _signalPeriods + 2)
            return NeutralResult("MACD sequence too short for signal line", context.TimestampUtc);

        // Compute signal line (EMA of MACD line)
        var signalSeq = ComputeEmaSequence(macdSeq, _signalPeriods);

        if (signalSeq.Count < 2)
            return NeutralResult("Signal line sequence too short", context.TimestampUtc);

        // Get current and previous values
        var macdNow = macdSeq[^1];
        var macdPrev = macdSeq[^2];
        var signalNow = signalSeq[^1];
        var signalPrev = signalSeq[^2];

        // Calculate histogram (MACD - Signal)
        var histNow = macdNow - signalNow;
        var histPrev = macdPrev - signalPrev;

        // Calculate histogram range for confidence normalization
        var recentHistograms = new List<decimal>();
        for (int i = Math.Max(0, macdSeq.Count - 20); i < macdSeq.Count && i < signalSeq.Count; i++)
        {
            if (i < signalSeq.Count)
                recentHistograms.Add(macdSeq[i] - signalSeq[i]);
        }

        var histRange = recentHistograms.Any()
            ? recentHistograms.Max() - recentHistograms.Min()
            : 1m;
        if (histRange == 0) histRange = 1m;

        // PRIMARY SIGNAL: Histogram crosses zero line
        bool histCrossUp = histPrev <= 0 && histNow > 0;
        bool histCrossDown = histPrev >= 0 && histNow < 0;

        if (histCrossUp)
        {
            var confidence = Math.Min(1.0, 0.5 + (double)(Math.Abs(histNow) / histRange) * 0.5);

            return new SignalResult
            {
                Direction = SignalDirection.Long,
                Confidence = confidence,
                Reason = $"MACD histogram cross up: Hist={histNow:F5}, MACD={macdNow:F5}, Signal={signalNow:F5}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["MACDCurrent"] = macdNow,
                    ["MACDPrevious"] = macdPrev,
                    ["SignalCurrent"] = signalNow,
                    ["SignalPrevious"] = signalPrev,
                    ["HistogramCurrent"] = histNow,
                    ["HistogramPrevious"] = histPrev,
                    ["HistogramRange"] = histRange,
                    ["SignalType"] = "HistogramCrossUp",
                    ["FastPeriods"] = _fastPeriods,
                    ["SlowPeriods"] = _slowPeriods,
                    ["SignalPeriods"] = _signalPeriods
                }
            };
        }

        if (histCrossDown)
        {
            var confidence = Math.Min(1.0, 0.5 + (double)(Math.Abs(histNow) / histRange) * 0.5);

            return new SignalResult
            {
                Direction = SignalDirection.Short,
                Confidence = confidence,
                Reason = $"MACD histogram cross down: Hist={histNow:F5}, MACD={macdNow:F5}, Signal={signalNow:F5}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["MACDCurrent"] = macdNow,
                    ["MACDPrevious"] = macdPrev,
                    ["SignalCurrent"] = signalNow,
                    ["SignalPrevious"] = signalPrev,
                    ["HistogramCurrent"] = histNow,
                    ["HistogramPrevious"] = histPrev,
                    ["HistogramRange"] = histRange,
                    ["SignalType"] = "HistogramCrossDown",
                    ["FastPeriods"] = _fastPeriods,
                    ["SlowPeriods"] = _slowPeriods,
                    ["SignalPeriods"] = _signalPeriods
                }
            };
        }

        // SECONDARY SIGNAL: Momentum expanding (histogram growing in magnitude)
        bool histExpanding = Math.Abs(histNow) > Math.Abs(histPrev);
        bool macdAboveSignal = macdNow > signalNow;
        bool macdBelowSignal = macdNow < signalNow;

        if (histExpanding && macdAboveSignal && histNow > 0)
        {
            var confidence = Math.Min(1.0, 0.4 + (double)(Math.Abs(histNow) / histRange) * 0.4);

            return new SignalResult
            {
                Direction = SignalDirection.Long,
                Confidence = confidence,
                Reason =
                    $"MACD momentum strengthening: Hist expanding={histNow:F5}, MACD={macdNow:F5} > Signal={signalNow:F5}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["MACDCurrent"] = macdNow,
                    ["SignalCurrent"] = signalNow,
                    ["HistogramCurrent"] = histNow,
                    ["HistogramPrevious"] = histPrev,
                    ["HistogramRange"] = histRange,
                    ["SignalType"] = "MomentumExpanding",
                    ["Direction"] = "Bullish"
                }
            };
        }

        if (histExpanding && macdBelowSignal && histNow < 0)
        {
            var confidence = Math.Min(1.0, 0.4 + (double)(Math.Abs(histNow) / histRange) * 0.4);

            return new SignalResult
            {
                Direction = SignalDirection.Short,
                Confidence = confidence,
                Reason =
                    $"MACD momentum strengthening: Hist contracting={histNow:F5}, MACD={macdNow:F5} < Signal={signalNow:F5}",
                GeneratedAt = context.TimestampUtc,
                Diagnostics = new Dictionary<string, object>
                {
                    ["MACDCurrent"] = macdNow,
                    ["SignalCurrent"] = signalNow,
                    ["HistogramCurrent"] = histNow,
                    ["HistogramPrevious"] = histPrev,
                    ["HistogramRange"] = histRange,
                    ["SignalType"] = "MomentumExpanding",
                    ["Direction"] = "Bearish"
                }
            };
        }

        // No actionable signal
        return NeutralResult(
            $"No MACD signal: Hist={histNow:F5}, MACD={macdNow:F5}, Signal={signalNow:F5}",
            context.TimestampUtc);
    }
}