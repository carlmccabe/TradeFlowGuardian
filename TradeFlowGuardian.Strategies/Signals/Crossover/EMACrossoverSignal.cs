using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Signals.Base;

namespace TradeFlowGuardian.Strategies.Signals.Crossover;

/// <summary>
/// Generates signals when a fast Exponential Moving Average (EMA) crosses a slow EMA.
/// </summary>
/// <remarks>
/// <para>
/// The EMACrossoverSignal is a momentum-based trend-following indicator that identifies potential
/// trend changes or continuations by detecting when a shorter-period EMA crosses above or below
/// a longer-period EMA. Unlike Simple Moving Average (SMA) crossovers, EMA crossovers are more
/// responsive to recent price action due to exponential weighting, making them better suited for
/// shorter timeframes and faster-moving markets.
/// </para>
/// <para>
/// Strategy Logic:
/// </para>
/// <list type="number">
///   <item><description>Computes fast EMA (shorter period, e.g., 10) on closing prices</description></item>
///   <item><description>Computes slow EMA (longer period, e.g., 30) on closing prices</description></item>
///   <item><description>Detects crossover by comparing current and previous bar relationships</description></item>
///   <item><description>Generates Long signal when fast EMA crosses above slow EMA (bullish crossover)</description></item>
///   <item><description>Generates Short signal when fast EMA crosses below slow EMA (bearish crossover)</description></item>
///   <item><description>Returns Neutral when EMAs are aligned but no crossover occurs</description></item>
/// </list>
/// <para>
/// EMA vs SMA Comparison:
/// </para>
/// <list type="bullet">
///   <item><description>Responsiveness: EMA reacts faster to price changes (exponential weighting vs equal weighting)</description></item>
///   <item><description>Lag: EMA has less lag, signals trend changes earlier</description></item>
///   <item><description>False signals: EMA generates more false signals in choppy markets</description></item>
///   <item><description>Best for: EMA suits intraday/swing trading; SMA suits position/long-term trading</description></item>
///   <item><description>Whipsaws: EMA more prone to whipsaws; SMA more resistant but slower</description></item>
/// </list>
/// <para>
/// Confidence Calculation:
/// </para>
/// <para>
/// Signal confidence is based on the separation between the two EMAs at the crossover point:
/// </para>
/// <list type="bullet">
///   <item><description>Base confidence: 0.5 (50%) for any valid crossover detection</description></item>
///   <item><description>Separation bonus: Up to +0.5 based on percentage separation between EMAs</description></item>
///   <item><description>Formula: Confidence = Min(1.0, 0.5 + (|FastEMA - SlowEMA| / SlowEMA) × 50)</description></item>
///   <item><description>Rationale: Wider separation = stronger momentum = higher conviction</description></item>
/// </list>
/// <para>
/// For example:
/// </para>
/// <list type="bullet">
///   <item><description>0.1% separation → ~55% confidence (weak crossover, may be noise)</description></item>
///   <item><description>0.5% separation → ~75% confidence (moderate crossover)</description></item>
///   <item><description>1.0%+ separation → ~100% confidence (strong crossover, established momentum)</description></item>
/// </list>
/// <para>
/// Crossover Detection Logic:
/// </para>
/// <para>
/// A crossover is detected by comparing the relationship between fast and slow EMAs across two consecutive bars:
/// </para>
/// <list type="bullet">
///   <item><description>Bullish crossover: FastEMA[t-1] ≤ SlowEMA[t-1] AND FastEMA[t] &gt; SlowEMA[t]</description></item>
///   <item><description>Bearish crossover: FastEMA[t-1] ≥ SlowEMA[t-1] AND FastEMA[t] &lt; SlowEMA[t]</description></item>
/// </list>
/// <para>
/// This ensures we capture the exact bar where the crossover occurs, not just divergence.
/// </para>
/// <para>
/// Common Period Combinations:
/// </para>
/// <list type="bullet">
///   <item><description>Scalping (1m-5m): EMA(5, 15) or EMA(8, 21) - very fast, high noise</description></item>
///   <item><description>Day trading (15m-1H): EMA(10, 30) or EMA(12, 26) - balanced responsiveness</description></item>
///   <item><description>Swing trading (4H-Daily): EMA(20, 50) or EMA(9, 21) - smoother trends</description></item>
///   <item><description>Position trading (Daily-Weekly): EMA(50, 200) - major trend identification</description></item>
///   <item><description>MACD-inspired: EMA(12, 26) - standard MACD parameters</description></item>
/// </list>
/// <para>
/// Usage Examples:
/// </para>
/// <code>
/// // Example 1: Standard day trading setup
/// var dayTrading = new EMACrossoverSignal(
///     id: "ema_cross_10_30",
///     fastPeriods: 10,
///     slowPeriods: 30
/// );
/// 
/// // Example 2: MACD-style parameters for swing trading
/// var swingTrading = new EMACrossoverSignal(
///     id: "ema_cross_12_26",
///     fastPeriods: 12,
///     slowPeriods: 26
/// );
/// 
/// // Example 3: Golden/Death cross (long-term trends)
/// var positionTrading = new EMACrossoverSignal(
///     id: "ema_golden_cross",
///     fastPeriods: 50,
///     slowPeriods: 200
/// );
/// 
/// // Example 4: Using the signal with confidence threshold
/// var result = dayTrading.Generate(context);
/// if (result.Direction == SignalDirection.Long &amp;&amp; result.Confidence > 0.65)
/// {
///     Console.WriteLine($"Strong bullish momentum detected!");
///     Console.WriteLine($"EMA separation: {result.Diagnostics["Separation"]:F5}");
///     Console.WriteLine($"Fast EMA: {result.Diagnostics["FastCurrent"]}");
///     Console.WriteLine($"Slow EMA: {result.Diagnostics["SlowCurrent"]}");
/// }
/// </code>
/// <para>
/// Parameter Tuning Guidelines:
/// </para>
/// <list type="bullet">
///   <item><description>Period ratio: Slow/Fast ratio of 2.5-3.5 works well (e.g., 10/30, 12/36)</description></item>
///   <item><description>Shorter periods (5-20): More signals, higher noise, better for scalping</description></item>
///   <item><description>Medium periods (20-50): Balanced, good for day/swing trading</description></item>
///   <item><description>Longer periods (50-200): Fewer signals, higher quality, better for position trading</description></item>
///   <item><description>Market type: Trending markets = wider periods, ranging markets = tighter periods (but expect whipsaws)</description></item>
/// </list>
/// <para>
/// Optimal Market Conditions:
/// </para>
/// <list type="bullet">
///   <item><description>Best: Trending markets with clear directional bias and momentum</description></item>
///   <item><description>Good: Post-breakout scenarios where trend is establishing</description></item>
///   <item><description>Poor: Choppy/ranging markets (generates many false signals)</description></item>
///   <item><description>Avoid: Low volatility consolidations, tight trading ranges</description></item>
/// </list>
/// <para>
/// Limitations and Considerations:
/// </para>
/// <list type="bullet">
///   <item><description>Lagging indicator: Even EMA lags price; crossovers occur after trend has started</description></item>
///   <item><description>Whipsaw prone: Generates false signals in sideways markets (30-50% of trading time)</description></item>
///   <item><description>Late entries: Often enters after optimal entry point, misses initial trend momentum</description></item>
///   <item><description>No magnitude info: Doesn't indicate how far trend will run or when to exit</description></item>
///   <item><description>No reversal warning: Crossover indicates trend change but not exhaustion</description></item>
/// </list>
/// <para>
/// Complementary Signals and Filters:
/// </para>
/// <para>
/// EMA crossover signals are significantly more effective when combined with:
/// </para>
/// <list type="bullet">
///   <item><description>TrendFilter: Only take crossovers in the direction of a higher timeframe trend (e.g., only long signals above 200 EMA)</description></item>
///   <item><description>RSI/Momentum: Confirm crossover with momentum divergence or overbought/oversold exit</description></item>
///   <item><description>Volume: Higher volume on crossover bar = stronger signal reliability</description></item>
///   <item><description>BreakoutSignal: Crossover + range breakout = high-probability trend start</description></item>
///   <item><description>ADX: Only trade crossovers when ADX &gt; 25 (trending market condition)</description></item>
///   <item><description>Price action: Look for crossovers at support/resistance levels for higher quality</description></item>
/// </list>
/// <para>
/// EMA vs SMA Crossover - When to Use Which:
/// </para>
/// <list type="bullet">
///   <item><description>Use EMA when: Trading shorter timeframes (≤4H), need faster entries, in volatile markets</description></item>
///   <item><description>Use SMA when: Trading longer timeframes (≥Daily), prioritize fewer false signals, in stable markets</description></item>
///   <item><description>Don't use both as parallel signals: They measure the same thing, create redundant correlation</description></item>
///   <item><description>Can use both hierarchically: SMA(50,200) for regime filter + EMA(10,30) for timing</description></item>
/// </list>
/// <para>
/// Historical Context:
/// </para>
/// <para>
/// EMA crossovers became popular in the 1970s-80s with the rise of technical analysis. The "Golden Cross" 
/// (50 EMA crossing above 200 EMA) and "Death Cross" (50 EMA crossing below 200 EMA) are widely watched by
/// institutional traders and often become self-fulfilling prophecies due to widespread adoption. However,
/// by the time these major crossovers occur, significant trend movement has usually already happened.
/// </para>
/// </remarks>
public sealed class EmaCrossoverSignal : SignalBase
{
    private readonly int _fastPeriods;
    private readonly int _slowPeriods;

    /// <summary>
    /// Initializes a new instance of the <see cref="id"/> class with specified period parameters.
    /// </summary>
    /// <param name="fastPeriods">
    /// Unique identifier for this signal instance. Used for diagnostics and signal composition.
    /// Convention: lowercase with underscores (e.g., "ema_cross_10_30", "ema_fast").
    /// </param>
    /// <param name="slowPeriods">
    /// Number of periods for the fast (shorter) EMA. Should be significantly less than slow periods.
    /// <list type="bullet">
    ///   <item><description>5-10: Very fast, scalping, high noise</description></item>
    ///   <item><description>10-20: Fast, day trading, balanced</description></item>
    ///   <item><description>20-50: Moderate, swing trading</description></item>
    ///   <item><description>50+: Slow, position trading</description></item>
    /// </list>
    /// Default: 10 periods.
    /// Valid range: 2-100 periods.
    /// </param>
    /// <param name="slowPeriods">
    /// Number of periods for the slow (longer) EMA. Should be 2.5-4× larger than fast periods for clear separation.
    /// <list type="bullet">
    ///   <item><description>15-30: Fast crossovers, scalping/day trading</description></item>
    ///   <item><description>30-100: Moderate crossovers, swing trading</description></item>
    ///   <item><description>100-200: Slow crossovers, major trends</description></item>
    /// </list>
    /// Default: 30 periods.
    /// Valid range: 5-300 periods.
    /// </param>
    /// <exception cref="ArgumentException">Thrown if id is null.</exception>
    /// <exception cref="EmaCrossoverSignal">
    /// Thrown if fastPeriods &lt; 2, slowPeriods &lt; fastPeriods, or slowPeriods &gt; 300.
    /// </exception>
    /// <example>
    /// <code>
    /// // Standard day trading setup
    /// var signal = new EMACrossoverSignal(
    ///     id: "ema_10_30",
    ///     fastPeriods: 10,
    ///     slowPeriods: 30
    /// );
    /// </code>
    /// </example>
    public EmaCrossoverSignal(string id, int fastPeriods = 10, int slowPeriods = 30)
        : base(id, $"EMACross({fastPeriods},{slowPeriods})")
    {
        if (fastPeriods < 2)
            throw new ArgumentException("Fast periods must be >= 2", nameof(fastPeriods));
        if (slowPeriods <= fastPeriods)
            throw new ArgumentException("Slow periods must be greater than fast periods", nameof(slowPeriods));
        if (slowPeriods > 300)
            throw new ArgumentException("Slow periods must be <= 300", nameof(slowPeriods));

        _fastPeriods = fastPeriods;
        _slowPeriods = slowPeriods;
    }

    /// <summary>
    /// Computes Exponential Moving Average (EMA) for a series of closing prices.
    /// </summary>
    /// <param name="prices">
    /// List of closing prices. Must contain at least the period count for meaningful EMA calculation.
    /// </param>
    /// <param name="period">
    /// Number of periods for EMA calculation. Determines the smoothing factor: K = 2 / (period + 1).
    /// </param>
    /// <returns>
    /// The EMA value for the most recent price in the series. Returns 0 if insufficient data.
    /// </returns>
    /// <remarks>
    /// <para>
    /// EMA Calculation Formula:
    /// </para>
    /// <para>
    /// EMA[t] = Price[t] × K + EMA[t-1] × (1 - K), where K = 2 / (period + 1)
    /// </para>
    /// <para>
    /// The smoothing factor K determines how much weight recent prices receive:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Shorter period → Higher K → More weight on recent prices → More responsive</description></item>
    ///   <item><description>Longer period → Lower K → More weight on older prices → Smoother, less responsive</description></item>
    /// </list>
    /// <para>
    /// Initialization: The first EMA value is seeded with a Simple Moving Average (SMA)
    /// over the first 'period' prices. This is standard practice to avoid arbitrary starting values.
    /// </para>
    /// <para>
    /// Note: This is an inline computation for simplicity. For production systems with multiple
    /// EMA-based signals, consider using a shared EMA indicator instance to avoid redundant calculations and
    /// improve performance, especially in backtesting scenarios.
    /// </para>
    /// </remarks>
    private static decimal ComputeEma(IReadOnlyList<decimal> prices, int period)
    {
        if (prices.Count < period) return 0m;

        // Seed with SMA for the first period
        decimal ema = prices.Take(period).Average();

        // Smoothing multiplier: 2 / (period + 1)
        decimal k = 2m / (period + 1);

        // Apply exponential smoothing to remaining prices
        for (int i = period; i < prices.Count; i++)
        {
            ema = prices[i] * k + ema * (1 - k);
        }

        return ema;
    }

    /// <summary>
    /// Core signal generation logic that evaluates market context for EMA crossover conditions.
    /// </summary>
    /// <param name="context">
    /// Immutable market context containing price history, indicators, and timestamp.
    /// Must contain at least (slowPeriods + 2) candles for reliable crossover detection.
    /// </param>
    /// <returns>
    /// <see cref="SignalResult"/> with:
    /// <list type="bullet">
    ///   <item><description>Long signal: When fast EMA crosses above slow EMA with confidence based on separation</description></item>
    ///   <item><description>Short signal: When fast EMA crosses below slow EMA with confidence based on separation</description></item>
    ///   <item><description>Neutral signal: When no crossover detected or insufficient data</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// Data Requirements:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Need slowPeriods + 1 for current EMA calculation</description></item>
    ///   <item><description>Need +1 additional for previous bar comparison (total: slowPeriods + 2)</description></item>
    ///   <item><description>More data improves EMA stability (warmup period recommended)</description></item>
    /// </list>
    /// <para>
    /// Crossover Detection Process:
    /// </para>
    /// <list type="number">
    ///   <item><description>Extract closing prices from candles</description></item>
    ///   <item><description>Compute current bar EMAs (both fast and slow)</description></item>
    ///   <item><description>Compute previous bar EMAs (using closes[0..^1])</description></item>
    ///   <item><description>Compare relationships to detect crossover</description></item>
    ///   <item><description>Calculate confidence based on EMA separation</description></item>
    /// </list>
    /// <para>
    /// Early Exit Conditions:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Insufficient data: Returns neutral if candle count &lt; (slowPeriods + 2)</description></item>
    ///   <item><description>No crossover: Returns neutral if EMAs maintain same relationship on both bars</description></item>
    /// </list>
    /// <para>
    /// Diagnostic Information:
    /// </para>
    /// <para>
    /// All signal results include comprehensive diagnostics for analysis:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>FastCurrent/FastPrevious: Fast EMA values on current and previous bars</description></item>
    ///   <item><description>SlowCurrent/SlowPrevious: Slow EMA values on current and previous bars</description></item>
    ///   <item><description>Separation: Absolute difference between fast and slow EMAs</description></item>
    ///   <item><description>SeparationPercent: Separation as percentage of slow EMA (normalized measure)</description></item>
    ///   <item><description>FastPeriods/SlowPeriods: Configuration parameters for traceability</description></item>
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

        // Extract closing prices for EMA calculation
        var closes = context.Candles.Select(c => c.Close).ToList();
        var prevCloses = closes.Take(closes.Count - 1).ToList();

        // Compute current bar EMAs
        var fastNow = ComputeEma(closes, _fastPeriods);
        var slowNow = ComputeEma(closes, _slowPeriods);

        // Compute previous bar EMAs for crossover detection
        var fastPrev = ComputeEma(prevCloses, _fastPeriods);
        var slowPrev = ComputeEma(prevCloses, _slowPeriods);

        // Detect bullish crossover: fast was below or equal, now above
        bool bullishCross = fastPrev <= slowPrev && fastNow > slowNow;

        // Detect bearish crossover: fast was above or equal, now below
        bool bearishCross = fastPrev >= slowPrev && fastNow < slowNow;

        if (bullishCross)
        {
            // Calculate separation and confidence
            var separation = fastNow - slowNow;
            var separationPercent = (double)(separation / slowNow);

            // Confidence: base 0.5 + bonus up to 0.5 based on percentage separation
            // 1% separation = 100% confidence, scales linearly
            var confidence = Math.Min(1.0, 0.5 + Math.Abs(separationPercent) * 50.0);

            return new SignalResult
            {
                Direction = SignalDirection.Long,
                Confidence = confidence,
                Reason =
                    $"Bullish EMA crossover: Fast={fastNow:F5} crossed above Slow={slowNow:F5}, Separation={separationPercent:P2}",
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
                    ["SlowPeriods"] = _slowPeriods
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

            return new SignalResult
            {
                Direction = SignalDirection.Short,
                Confidence = confidence,
                Reason =
                    $"Bearish EMA crossover: Fast={fastNow:F5} crossed below Slow={slowNow:F5}, Separation={separationPercent:P2}",
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
                    ["SlowPeriods"] = _slowPeriods
                }
            };
        }

        // No crossover detected: EMAs aligned but no crossing event
        return NeutralResult(
            $"No crossover: Fast={fastNow:F5}, Slow={slowNow:F5}, FastPrev={fastPrev:F5}, SlowPrev={slowPrev:F5}",
            context.TimestampUtc);
    }
}