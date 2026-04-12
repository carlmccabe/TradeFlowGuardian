namespace TradeFlowGuardian.Domain.Entities.Strategies.Core;

/// <summary>
/// Generates a trading signal representing directional intent with confidence.
/// </summary>
/// <remarks>
/// <para>
/// Signals are the "what" component in a trading strategy - they analyze market conditions
/// and produce a directional bias (Long, Short, or Neutral) along with a confidence measure.
/// Rules and filters then combine signals into "when" decisions about actual trade execution.
/// </para>
/// <para>
/// Design Philosophy:
/// </para>
/// <list type="bullet">
///   <item><description>Pure evaluation: Signals should be stateless and deterministic given the same market context.</description></item>
///   <item><description>Single responsibility: Each signal should represent one coherent market view or setup.</description></item>
///   <item><description>Composable: Multiple signals can be combined using weighted aggregation or logical operations.</description></item>
///   <item><description>Observable: Rich diagnostics enable backtesting analysis and live debugging.</description></item>
/// </list>
/// <para>
/// Usage Examples:
/// </para>
/// <para>
/// Example 1: Creating a simple crossover signal
/// </para>
/// <code>
/// var crossover = new CrossoverSignal(
///     id: "ema_cross_10_30",
///     fastIndicatorId: "ema_10",
///     slowIndicatorId: "ema_30"
/// );
/// 
/// var result = crossover.Generate(marketContext);
/// 
/// if (result.Direction == SignalDirection.Long &amp;&amp; result.Confidence > 0.6)
/// {
///     Console.WriteLine($"Strong bullish signal: {result.Reason}");
/// }
/// </code>
/// <para>
/// Example 2: Implementing a custom signal
/// </para>
/// <code>
/// public class CustomMomentumSignal : SignalBase
/// {
///     private readonly int _lookback;
///     
///     public CustomMomentumSignal(string id, int lookback = 10)
///         : base(id, "CustomMomentum")
///     {
///         _lookback = lookback;
///     }
///     
///     protected override SignalResult GenerateCore(IMarketContext context)
///     {
///         if (context.Candles.Count &lt; _lookback)
///             return NeutralResult("Insufficient data", context.TimestampUtc);
///         
///         var currentPrice = context.Candles[^1].Close;
///         var pastPrice = context.Candles[^_lookback].Close;
///         var momentum = (double)((currentPrice - pastPrice) / pastPrice);
///         
///         if (momentum > 0.02) // 2% gain
///         {
///             return new SignalResult
///             {
///                 Direction = SignalDirection.Long,
///                 Confidence = Math.Min(1.0, Math.Abs(momentum) * 10),
///                 Reason = $"Strong upward momentum: {momentum:P2}",
///                 GeneratedAt = context.TimestampUtc,
///                 Diagnostics = new Dictionary&lt;string, object&gt;
///                 {
///                     ["Momentum"] = momentum,
///                     ["CurrentPrice"] = currentPrice,
///                     ["PastPrice"] = pastPrice
///                 }
///             };
///         }
///         
///         return NeutralResult("Momentum below threshold", context.TimestampUtc);
///     }
/// }
/// </code>
/// <para>
/// Example 3: Combining multiple signals with weighted aggregation
/// </para>
/// <code>
/// var trendSignal = new CrossoverSignal("trend", "ema_50", "ema_200");
/// var momentumSignal = new RSISignal("momentum", "rsi_14");
/// var breakoutSignal = new BreakoutSignal("breakout", lookbackPeriods: 20);
/// 
/// var aggregator = new WeightedSignalAggregator(
///     id: "composite_signal",
///     signals: new[] 
///     {
///         (trendSignal, 0.5),      // 50% weight on trend
///         (momentumSignal, 0.3),   // 30% weight on momentum
///         (breakoutSignal, 0.2)    // 20% weight on breakout
///     }
/// );
/// 
/// var compositeResult = aggregator.Generate(marketContext);
/// // compositeResult.Confidence is the weighted average of component confidences
/// </code>
/// </remarks>
public interface ISignal
{
    /// <summary>
    /// Unique identifier for this signal instance.
    /// </summary>
    /// <remarks>
    /// Used for diagnostics, logging, and signal composition. Should be unique within a strategy
    /// to enable tracing which signal contributed to a decision. Convention: use lowercase with
    /// underscores (e.g., "ema_cross_10_30", "rsi_oversold").
    /// </remarks>
    string Id { get; }

    /// <summary>
    /// Categorical name describing the signal type or strategy pattern.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Represents the category or family of this signal (e.g., "Crossover", "Breakout", 
    /// "MeanReversion", "Momentum"). Multiple signals can share the same SignalType but
    /// have different IDs and parameters.
    /// </para>
    /// <para>
    /// Common signal types:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Crossover: Moving average or indicator line crosses</description></item>
    ///   <item><description>Breakout: Price breaks through support/resistance</description></item>
    ///   <item><description>MeanReversion: Price deviates significantly from mean</description></item>
    ///   <item><description>Momentum: Rate of price change or oscillator levels</description></item>
    ///   <item><description>PatternRecognition: Chart patterns (e.g., double top, head and shoulders)</description></item>
    ///   <item><description>Composite: Aggregation of multiple signal types</description></item>
    /// </list>
    /// </remarks>
    string SignalType { get; }

    /// <summary>
    /// Generate a trading signal based on the current market context.
    /// </summary>
    /// <param name="context">
    /// Immutable snapshot of market data and pre-computed indicators. Contains price candles,
    /// technical indicators, account state, and timestamp for deterministic evaluation.
    /// </param>
    /// <returns>
    /// A <see cref="SignalResult"/> containing direction (Long/Short/Neutral), confidence level,
    /// reasoning, optional stop loss and take profit suggestions, and diagnostic data.
    /// Returns a neutral signal (Direction = Neutral, Confidence = 0) when conditions are not met
    /// or data is insufficient.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Implementation Guidelines:
    /// </para>
    /// <list type="number">
    ///   <item><description>Deterministic: Same context must always produce the same result. No random number generation or system clock access.</description></item>
    ///   <item><description>Safe: Handle insufficient data gracefully by returning neutral signal with explanatory reason.</description></item>
    ///   <item><description>Fast: Optimize for performance as this may be called thousands of times in backtests.</description></item>
    ///   <item><description>Observable: Include rich diagnostics for debugging and analysis.</description></item>
    ///   <item><description>Normalized confidence: Confidence should be in range [0.0, 1.0] where 0 = no confidence, 1 = maximum confidence.</description></item>
    /// </list>
    /// <para>
    /// Error Handling:
    /// </para>
    /// <para>
    /// Implementations should NOT throw exceptions for business logic conditions (e.g., insufficient data).
    /// Instead, return a neutral signal with appropriate reason. Only throw for programming errors
    /// (e.g., null context). Consider inheriting from SignalBase which provides exception
    /// handling and converts exceptions to neutral results with diagnostic information.
    /// </para>
    /// <para>
    /// Performance Considerations:
    /// </para>
    /// <para>
    /// Signals should rely on pre-computed indicators in the context rather than recalculating them.
    /// If an indicator is missing, return neutral with "MissingIndicator" reason rather than
    /// attempting to compute it inline.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if context is null (implementation-dependent).</exception>
    /// <example>
    /// Handling insufficient data:
    /// <code>
    /// public SignalResult Generate(IMarketContext context)
    /// {
    ///     if (context.Candles.Count &lt; _requiredHistory)
    ///     {
    ///         return new SignalResult
    ///         {
    ///             Direction = SignalDirection.Neutral,
    ///             Confidence = 0.0,
    ///             Reason = $"InsufficientData: Need {_requiredHistory}, have {context.Candles.Count}",
    ///             GeneratedAt = context.TimestampUtc
    ///         };
    ///     }
    ///     
    ///     // ... signal logic ...
    /// }
    /// </code>
    /// </example>
    SignalResult Generate(IMarketContext context);
}

/// <summary>
/// Result of signal generation containing direction, confidence, and supporting information.
/// </summary>
/// <remarks>
/// <para>
/// SignalResult is an immutable record that encapsulates all information about a generated
/// trading signal. It supports both algorithmic decision-making (via Direction and Confidence)
/// and human analysis (via Reason and Diagnostics).
/// </para>
/// <para>
/// Confidence Interpretation:
/// </para>
/// <list type="bullet">
///   <item><description>0.0 - 0.3: Weak signal, typically ignored or used only with strong confirmation</description></item>
///   <item><description>0.3 - 0.6: Moderate signal, may be traded with appropriate risk management</description></item>
///   <item><description>0.6 - 0.8: Strong signal, high probability setup</description></item>
///   <item><description>0.8 - 1.0: Very strong signal, rare but high-conviction situations</description></item>
/// </list>
/// <para>
/// Stop Loss and Take Profit:
/// </para>
/// <para>
/// Signals can optionally suggest exit levels based on their analysis (e.g., below recent swing
/// low for long positions, at resistance level for take profit). These are suggestions; the
/// actual position sizing and risk management is handled by the portfolio manager.
/// </para>
/// </remarks>
public sealed record SignalResult
{
    /// <summary>
    /// Direction of the signal indicating intended position bias.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description>Neutral (0): No directional bias, do not enter a position</description></item>
    ///   <item><description>Long (1): Bullish bias, consider buying or closing shorts</description></item>
    ///   <item><description>Short (-1): Bearish bias, consider selling or closing longs</description></item>
    /// </list>
    /// </remarks>
    public SignalDirection Direction { get; init; }

    /// <summary>
    /// Confidence level of the signal, normalized to range [0.0, 1.0].
    /// </summary>
    /// <remarks>
    /// <para>
    /// Represents the signal's conviction about the predicted direction. Higher confidence
    /// typically results in larger position sizes or more aggressive entry/exit timing.
    /// </para>
    /// <para>
    /// Confidence of 0.0 with non-Neutral direction indicates the signal detected a setup
    /// but has no conviction (unusual but valid). Confidence greater than 0.0 with Neutral direction
    /// is semantically invalid and should be avoided.
    /// </para>
    /// <para>
    /// Calculation approaches:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Distance-based: Normalize how far price/indicator is from threshold</description></item>
    ///   <item><description>Probability-based: Historical win rate for similar setups</description></item>
    ///   <item><description>Multi-factor: Combine multiple confirming/conflicting factors</description></item>
    ///   <item><description>Fixed: Use constant confidence for binary signals (e.g., 0.7 for any crossover)</description></item>
    /// </list>
    /// </remarks>
    public double Confidence { get; init; }

    /// <summary>
    /// Human-readable explanation of why this signal was generated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Should be concise but informative, suitable for display in logs and UI. Include key
    /// metric values that drove the decision. Use consistent codes for common scenarios
    /// (e.g., "InsufficientData", "BullishCrossover", "OverboughtCondition").
    /// </para>
    /// <para>
    /// Good reasons:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>"Bullish crossover: EMA(10)=1.2543 crossed above EMA(30)=1.2501"</description></item>
    ///   <item><description>"Breakout above resistance: Price=1.3200, Resistance=1.3150"</description></item>
    ///   <item><description>"InsufficientData: Need 50 candles, have 30"</description></item>
    /// </list>
    /// <para>
    /// Avoid:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Empty or generic messages like "Signal generated"</description></item>
    ///   <item><description>Overly verbose messages with full stack traces (use Diagnostics instead)</description></item>
    /// </list>
    /// </remarks>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Optional suggested stop loss level for risk management.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When provided, indicates where the signal's logic suggests invalidation of the setup
    /// (e.g., below recent swing low for longs, above recent swing high for shorts). Null
    /// indicates no suggestion; the portfolio manager should use default risk rules.
    /// </para>
    /// <para>
    /// This is expressed as an absolute price level, not a distance or percentage.
    /// </para>
    /// </remarks>
    public decimal? SuggestedStopLoss { get; init; }

    /// <summary>
    /// Optional suggested take profit level for profit target.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When provided, indicates a logical profit target based on the signal's analysis
    /// (e.g., previous resistance level, Fibonacci extension, risk-reward multiple).
    /// Null indicates no suggestion.
    /// </para>
    /// <para>
    /// This is expressed as an absolute price level, not a distance or percentage.
    /// Multiple take profit levels are not directly supported; use Diagnostics to store
    /// additional targets if needed.
    /// </para>
    /// </remarks>
    public decimal? SuggestedTakeProfit { get; init; }

    /// <summary>
    /// Additional diagnostic information for debugging and analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Key-value pairs providing detailed insights into signal calculation. Useful for:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Backtesting analysis: Understanding why signals succeeded or failed</description></item>
    ///   <item><description>Parameter optimization: Correlating diagnostic values with outcomes</description></item>
    ///   <item><description>Live debugging: Inspecting intermediate calculations</description></item>
    ///   <item><description>Audit trails: Recording exact market conditions at signal time</description></item>
    /// </list>
    /// <para>
    /// Common diagnostic keys:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>"FastValue", "SlowValue": Indicator values in crossover signals</description></item>
    ///   <item><description>"Threshold", "ActualValue": Comparison values for threshold-based signals</description></item>
    ///   <item><description>"ATR", "Volatility": Market condition metrics</description></item>
    ///   <item><description>"Exception": Full exception details if signal generation failed</description></item>
    ///   <item><description>"ComponentSignals": Sub-signal results in composite signals</description></item>
    /// </list>
    /// <para>
    /// Values should be JSON-serializable primitives, arrays, or dictionaries. Avoid storing
    /// large objects or circular references.
    /// </para>
    /// </remarks>
    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// UTC timestamp when this signal was generated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Should match context.TimestampUtc passed to Generate(). This enables:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Correlating signals with candle timestamps in backtests</description></item>
    ///   <item><description>Measuring signal staleness (time since generation vs. execution)</description></item>
    ///   <item><description>Aligning signals from multiple symbols in portfolio strategies</description></item>
    /// </list>
    /// <para>
    /// Always use UTC to avoid timezone confusion. For display purposes, convert to local
    /// time in the presentation layer.
    /// </para>
    /// </remarks>
    public DateTime GeneratedAt { get; init; }
}

/// <summary>
/// Enumeration of possible signal directions.
/// </summary>
/// <remarks>
/// <para>
/// Integer values enable arithmetic operations (e.g., summing directions for aggregation,
/// multiplying direction by position size). The values are:
/// </para>
/// <list type="bullet">
///   <item><description>Neutral = 0: No position or bias</description></item>
///   <item><description>Long = 1: Positive/bullish bias</description></item>
///   <item><description>Short = -1: Negative/bearish bias</description></item>
/// </list>
/// <para>
/// This makes direction aggregation intuitive: (Long + Short) / 2 = Neutral
/// </para>
/// </remarks>
public enum SignalDirection
{
    /// <summary>
    /// No directional bias; do not enter a position or remain flat.
    /// </summary>
    Neutral = 0,

    /// <summary>
    /// Bullish bias; consider buying or holding long positions.
    /// </summary>
    Long = 1,

    /// <summary>
    /// Bearish bias; consider selling or holding short positions.
    /// </summary>
    Short = -1
}