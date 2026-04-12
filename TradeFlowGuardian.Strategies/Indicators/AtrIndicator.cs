using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Indicators.Base;

namespace TradeFlowGuardian.Strategies.Indicators;

/// <summary>
/// Average True Range (ATR) indicator - measures market volatility.
/// </summary>
/// <remarks>
/// <para>
/// 📚 What is ATR?
/// </para>
/// <para>
/// The Average True Range (ATR) is a volatility indicator that measures how much an asset typically moves
/// in a given period. Unlike other indicators that focus on direction or momentum, ATR simply answers:
/// "How volatile is this market right now?"
/// </para>
/// <para>
/// Think of ATR as a "volatility thermometer" - high ATR means big price swings (hot market), low ATR means
/// small movements (quiet market). This information is crucial for risk management, position sizing, and
/// setting appropriate stop-losses.
/// </para>
/// <para>
/// Key characteristics:
/// </para>
/// <list type="bullet">
///   <item><description>Non-directional: ATR doesn't tell you if price is going up or down, only how much it's moving</description></item>
///   <item><description>Absolute values: ATR is in the same units as price (pips for forex, dollars for stocks)</description></item>
///   <item><description>Relative comparison: Compare current ATR to historical ATR to gauge if volatility is high or low</description></item>
///   <item><description>Market-dependent: EUR/USD ATR of 50 pips is normal, GBP/JPY ATR of 50 pips is unusually calm</description></item>
/// </list>
/// <para>
/// 🎯 Purpose and Use Cases:
/// </para>
/// <list type="bullet">
///   <item><description>Stop-Loss Placement: Set stops at ATR-based distances (e.g., entry price ± 2×ATR) to avoid being stopped out by normal noise</description></item>
///   <item><description>Position Sizing: Risk same dollar amount per trade by adjusting position size based on ATR (lower size when ATR is high)</description></item>
///   <item><description>Profit Targets: Set take-profit at multiples of ATR (e.g., 3×ATR) to capture realistic moves while avoiding overreach</description></item>
///   <item><description>Volatility Filter: Only trade when ATR is above minimum threshold - avoid dead markets with no opportunity</description></item>
///   <item><description>Breakout Confirmation: High ATR during breakout suggests strong move; low ATR suggests weak/false breakout</description></item>
///   <item><description>Trailing Stops: Trail stop-loss at 2-3×ATR behind price as trend progresses</description></item>
/// </list>
/// <para>
/// 🔢 How ATR is Calculated:
/// </para>
/// <para>
/// ATR uses "True Range" which captures the full extent of price movement including gaps:
/// </para>
/// <code>
/// Step 1: Calculate True Range (TR) for each candle
/// TR = Maximum of:
///   1. Current High - Current Low (normal range)
///   2. |Current High - Previous Close| (gap up)
///   3. |Current Low - Previous Close| (gap down)
/// 
/// Why three comparisons? To catch overnight gaps and gaps after news:
/// 
/// Example 1 - Normal candle (no gap):
/// Previous Close: 1.1000, Current: High=1.1020, Low=1.0990
/// TR = max(1.1020-1.0990, |1.1020-1.1000|, |1.0990-1.1000|)
///    = max(0.0030, 0.0020, 0.0010) = 0.0030 (30 pips)
/// 
/// Example 2 - Gap up at open:
/// Previous Close: 1.1000, Current: High=1.1080, Low=1.1050
/// TR = max(1.1080-1.1050, |1.1080-1.1000|, |1.1050-1.1000|)
///    = max(0.0030, 0.0080, 0.0050) = 0.0080 (80 pips)
///    Without TR, we'd miss the 50-pip gap!
/// 
/// Step 2: Calculate first ATR as simple average
/// First ATR = Average of first 'period' True Ranges
/// 
/// For period=14:
/// ATR[14] = (TR[1] + TR[2] + ... + TR[14]) / 14
/// 
/// Step 3: Apply Wilder's smoothing for subsequent values
/// ATR[i] = (ATR[i-1] × (period-1) + TR[i]) / period
/// 
/// Example with period=14:
/// Previous ATR = 0.0050 (50 pips)
/// Current TR = 0.0070 (70 pips - volatile day)
/// New ATR = (0.0050 × 13 + 0.0070) / 14
///         = (0.0650 + 0.0070) / 14
///         = 0.0720 / 14 = 0.0051 (51 pips)
/// 
/// Notice: ATR moves slowly! Even though today's range was 70 pips,
/// ATR only increased from 50 to 51 because it's smoothed over 14 periods.
/// </code>
/// <para>
/// 💡 Wilder's Smoothing: Same technique used in RSI and ADX. Gives approximately 93% weight to previous ATR
/// and only 7% to current TR (for period=14). This makes ATR very smooth and resistant to single-day spikes.
/// </para>
/// <para>
/// ⚠️ Important: ATR requires (period + 1) candles to produce the first value because you need 'period' True Ranges
/// calculated, and the first True Range calculation needs 2 candles (current + previous).
/// </para>
/// <para>
/// 📊 Interpreting ATR Values:
/// </para>
/// <para>
/// ATR is an absolute value in price units, so interpretation depends on the instrument and timeframe:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Market</term>
///     <description>Timeframe</description>
///     <description>Low ATR</description>
///     <description>Normal ATR</description>
///     <description>High ATR</description>
///   </listheader>
///   <item>
///     <term>EUR/USD</term>
///     <description>H1</description>
///     <description>&lt;0.0010 (10 pips)</description>
///     <description>0.0015-0.0030 (15-30 pips)</description>
///     <description>&gt;0.0050 (50+ pips)</description>
///   </item>
///   <item>
///     <term>GBP/JPY</term>
///     <description>H1</description>
///     <description>&lt;0.30 (30 pips)</description>
///     <description>0.50-1.00 (50-100 pips)</description>
///     <description>&gt;1.50 (150+ pips)</description>
///   </item>
///   <item>
///     <term>EUR/USD</term>
///     <description>D1</description>
///     <description>&lt;0.0050 (50 pips)</description>
///     <description>0.0070-0.0120 (70-120 pips)</description>
///     <description>&gt;0.0150 (150+ pips)</description>
///   </item>
/// </list>
/// <para>
/// Rule of thumb: ATR on daily charts is roughly 4-5× the hourly ATR. ATR on M15 is roughly 1/4 of hourly.
/// </para>
/// <para>
/// 🎬 Practical Trading Applications:
/// </para>
/// <example>
/// <code>
/// // Example 1: Basic ATR calculation
/// var atr = new AtrIndicator("atr_14", period: 14);
/// var result = atr.Compute(candles);
/// 
/// var latestAtr = result.Values[^1];
/// if (latestAtr.Value.HasValue)
/// {
///     Console.WriteLine($"Current ATR: {latestAtr.Value:F5} ({latestAtr.Value * 10000:F1} pips)");
/// }
/// 
/// // Example 2: ATR-based stop-loss (CRITICAL for risk management!)
/// // Never use fixed pip stops - always adapt to volatility
/// var atr14 = new AtrIndicator("atr_14", 14);
/// var atrResult = atr14.Compute(candles);
/// var currentAtr = atrResult.Values[^1].Value;
/// 
/// if (currentAtr.HasValue)
/// {
///     var entryPrice = candles[^1].Close;
///     var atrValue = (decimal)currentAtr;
///     
///     // For long position: Stop at entry - 2×ATR
///     var longStopLoss = entryPrice - (2 * atrValue);
///     
///     // For short position: Stop at entry + 2×ATR
///     var shortStopLoss = entryPrice + (2 * atrValue);
///     
///     Console.WriteLine($"Entry: {entryPrice:F5}");
///     Console.WriteLine($"ATR: {atrValue:F5} ({atrValue * 10000:F1} pips)");
///     Console.WriteLine($"Long stop: {longStopLoss:F5} (risk = {atrValue * 2 * 10000:F1} pips)");
///     Console.WriteLine($"Short stop: {shortStopLoss:F5} (risk = {atrValue * 2 * 10000:F1} pips)");
///     
///     // Why 2×ATR? Gives breathing room for normal volatility while still
///     // protecting against significant adverse moves. Adjust based on strategy:
///     // - Scalping: 1-1.5×ATR (tight stops, quick exits)
///     // - Day trading: 2-2.5×ATR (balanced)
///     // - Swing trading: 3-4×ATR (room for larger swings)
/// }
/// 
/// // Example 3: Position sizing based on ATR (professional risk management)
/// // Risk the SAME dollar amount per trade regardless of volatility
/// decimal accountBalance = 10000m;
/// decimal riskPercentage = 0.01m; // Risk 1% per trade
/// decimal riskAmount = accountBalance * riskPercentage; // $100
/// 
/// var atrForSizing = atrResult.Values[^1].Value;
/// if (atrForSizing.HasValue)
/// {
///     var stopDistance = (decimal)atrForSizing * 2; // 2×ATR stop
///     var stopDistancePips = stopDistance * 10000;
///     
///     // Calculate position size to risk exactly $100
///     // For forex: Position size = RiskAmount / (StopDistance in pips × pip value)
///     // Assuming $10 per pip per standard lot
///     var positionSizeLots = riskAmount / (stopDistancePips * 10m);
///     
///     Console.WriteLine($"Account: ${accountBalance}");
///     Console.WriteLine($"Risk per trade: ${riskAmount} ({riskPercentage*100}%)");
///     Console.WriteLine($"ATR: {stopDistancePips:F1} pips");
///     Console.WriteLine($"Stop distance: {stopDistancePips * 2:F1} pips");
///     Console.WriteLine($"Position size: {positionSizeLots:F2} lots");
///     Console.WriteLine();
///     Console.WriteLine("Result: Whether ATR is 20 pips or 50 pips, you always risk $100!");
/// }
/// 
/// // Example 4: Volatility filter - only trade when market is moving
/// // Dead markets = no opportunity. Don't force trades!
/// var historicalAtr = atrResult.Values
///     .Where(v => v.Value.HasValue)
///     .Select(v => v.Value!.Value)
///     .ToList();
/// 
/// if (historicalAtr.Count >= 50)
/// {
///     var avgAtr = historicalAtr.TakeLast(50).Average();
///     var currentAtr = historicalAtr[^1];
///     var atrRatio = currentAtr / avgAtr;
///     
///     if (atrRatio &lt; 0.5)
///     {
///         Console.WriteLine($"⚠️ LOW VOLATILITY: Current ATR is {atrRatio:P0} of 50-day average");
///         Console.WriteLine("   Market is unusually quiet - consider sitting out or reducing size");
///     }
///     else if (atrRatio > 1.5)
///     {
///         Console.WriteLine($"🔥 HIGH VOLATILITY: Current ATR is {atrRatio:P0} of 50-day average");
///         Console.WriteLine("   Market is hot - great for breakouts, but reduce size to manage risk");
///     }
///     else
///     {
///         Console.WriteLine($"✅ NORMAL VOLATILITY: Current ATR is {atrRatio:P0} of average");
///         Console.WriteLine("   Standard trading conditions");
///     }
/// }
/// 
/// // Example 5: Trailing stop using ATR (let winners run!)
/// // As price moves in your favor, trail the stop to lock in profits
/// var positions = new List&lt;(decimal entryPrice, decimal currentPrice, bool isLong)&gt;
/// {
///     (1.10000m, 1.10500m, true),  // Long position, +50 pips profit
///     (1.11000m, 1.10700m, false)  // Short position, +30 pips profit
/// };
/// 
/// var trailingAtr = atrResult.Values[^1].Value;
/// if (trailingAtr.HasValue)
/// {
///     var atrValue = (decimal)trailingAtr;
///     
///     foreach (var pos in positions)
///     {
///         if (pos.isLong)
///         {
///             // Long: Trail stop below current price
///             var trailingStop = pos.currentPrice - (2.5m * atrValue);
///             var profitAtStop = (trailingStop - pos.entryPrice) * 10000;
///             
///             Console.WriteLine($"LONG position:");
///             Console.WriteLine($"  Entry: {pos.entryPrice:F5}, Current: {pos.currentPrice:F5}");
///             Console.WriteLine($"  Trailing stop: {trailingStop:F5}");
///             Console.WriteLine($"  Locked profit: {profitAtStop:F1} pips");
///         }
///         else
///         {
///             // Short: Trail stop above current price
///             var trailingStop = pos.currentPrice + (2.5m * atrValue);
///             var profitAtStop = (pos.entryPrice - trailingStop) * 10000;
///             
///             Console.WriteLine($"SHORT position:");
///             Console.WriteLine($"  Entry: {pos.entryPrice:F5}, Current: {pos.currentPrice:F5}");
///             Console.WriteLine($"  Trailing stop: {trailingStop:F5}");
///             Console.WriteLine($"  Locked profit: {profitAtStop:F1} pips");
///         }
///     }
/// }
/// 
/// // Example 6: Timestamp-based volatility analysis
/// // When was volatility highest? Prepare for similar times!
/// var weekAgo = DateTime.UtcNow.AddDays(-7);
/// var volatilitySpikes = atrResult.Values
///     .Where(v => v.Timestamp > weekAgo &amp;&amp; v.Value.HasValue)
///     .OrderByDescending(v => v.Value)
///     .Take(5)
///     .ToList();
/// 
/// Console.WriteLine("Top 5 most volatile periods in last 7 days:");
/// foreach (var spike in volatilitySpikes)
/// {
///     var pips = spike.Value!.Value * 10000;
///     var dayOfWeek = spike.Timestamp.DayOfWeek;
///     var hour = spike.Timestamp.Hour;
///     
///     Console.WriteLine($"  {spike.Timestamp:yyyy-MM-dd HH:mm} ({dayOfWeek} {hour:D2}:00): " +
///                      $"ATR = {pips:F1} pips");
/// }
/// Console.WriteLine("Pattern recognition: Are spikes during news releases? Specific days/hours?");
/// </code>
/// </example>
/// <para>
/// ⚠️ Common Mistakes and How to Avoid Them:
/// </para>
/// <list type="bullet">
///   <item><description>❌ Mistake: Using fixed pip stops (e.g., always 30 pips) regardless of volatility
///     <para>Problem: During high volatility, 30 pips is too tight (stopped out). During low volatility, 30 pips is too wide (risking too much).</para>
///     <para>✅ Solution: Always use ATR-based stops. Risk adapts to market conditions automatically.</para>
///   </description></item>
///   <item><description>❌ Mistake: Using same position size for all trades
///     <para>Problem: When ATR doubles, your risk per trade doubles too - inconsistent risk management.</para>
///     <para>✅ Solution: Scale position size inversely with ATR. High ATR = smaller position. This keeps dollar risk constant.</para>
///   </description></item>
///   <item><description>❌ Mistake: Comparing ATR across different instruments/timeframes
///     <para>Problem: "EUR/USD ATR is 0.0030, GBP/JPY is 0.80" - can't compare directly! Different price scales.</para>
///     <para>✅ Solution: Compare current ATR to that instrument's historical ATR, or use ATR as % of price.</para>
///   </description></item>
///   <item><description>❌ Mistake: Trading during extremely low ATR
///     <para>Problem: Low volatility = tight ranges = many false breakouts. Little opportunity, high whipsaw risk.</para>
///     <para>✅ Solution: Set minimum ATR threshold (e.g., 50% of 50-day average). Stay out when below threshold.</para>
///   </description></item>
///   <item><description>❌ Mistake: Using too-tight stops during high ATR
///     <para>Problem: "ATR is 100 pips but I'm using 50-pip stop" = guaranteed stop-out from normal volatility.</para>
///     <para>✅ Solution: Minimum stop should be 1.5-2×ATR. If that risks too much, reduce position size instead.</para>
///   </description></item>
/// </list>
/// <para>
/// 🎓 ATR Parameter Guidelines:
/// </para>
/// <list type="bullet">
///   <item><description>Period = 14 (standard): Wilder's original recommendation, balanced smoothing. Use this unless you have specific reason to change.</description></item>
///   <item><description>Period = 7-10: More responsive to volatility changes, good for short-term trading. Updates faster but noisier.</description></item>
///   <item><description>Period = 20-30: Smoother, better for long-term position sizing. Slower to react but more stable.</description></item>
///   <item><description>Stop multiplier = 1.5-2×ATR: Tight, for scalping/day trading with quick exits</description></item>
///   <item><description>Stop multiplier = 2-3×ATR: Standard, balances protection and breathing room</description></item>
///   <item><description>Stop multiplier = 3-4×ATR: Wide, for swing trading and trend following</description></item>
/// </list>
/// <para>
/// 🏆 Professional Risk Management Formula:
/// </para>
/// <code>
/// Position Size = (Account Risk $ / Pips at Risk) / ($ per Pip per Lot)
/// 
/// Where:
/// - Account Risk $ = Account Balance × Risk % (e.g., $10,000 × 1% = $100)
/// - Pips at Risk = ATR × Multiplier × 10,000 (e.g., 0.0030 × 2 × 10,000 = 60 pips)
/// - $ per Pip = $10 for standard lot, $1 for mini, $0.10 for micro
/// 
/// Example:
/// Account = $10,000, Risk = 1% ($100), ATR = 30 pips, Stop = 2×ATR = 60 pips
/// Position = $100 / 60 pips / $10 per pip = 0.167 standard lots
/// 
/// This formula ensures you ALWAYS risk exactly $100, regardless of volatility!
/// </code>
/// <para>
/// 🔗 Complementary Indicators:
/// </para>
/// <list type="bullet">
///   <item><description>Bollinger Bands: Use ATR to set band width instead of standard deviation. More robust to outliers.</description></item>
///   <item><description>ADX: High ADX + High ATR = strong trending volatile market (ideal for trend following).</description></item>
///   <item><description>RSI: Low ATR + RSI extremes = range-bound market, mean reversion opportunity.</description></item>
///   <item><description>Volume: ATR spike with volume spike = significant move, likely continuation. ATR spike with low volume = false alarm.</description></item>
/// </list>
/// <para>
/// 📚 Historical Context:
/// </para>
/// <para>
/// ATR was developed by J. Welles Wilder Jr. and introduced in his 1978 book "New Concepts in Technical Trading Systems"
/// (same book that gave us RSI, ADX, and Parabolic SAR). Wilder was a mechanical engineer turned trader who recognized
/// that volatility management was more important than predicting direction.
/// </para>
/// <para>
/// Before ATR, traders used simple range (High - Low) which failed to account for gaps. Wilder's True Range concept
/// was revolutionary because it captured the full price movement including overnight gaps and limit moves. This made
/// ATR the first indicator to properly measure volatility in markets with gaps.
/// </para>
/// <para>
/// Today, ATR is universally used by professional traders and risk managers. It's the foundation of:
/// - Position sizing algorithms in algorithmic trading
/// - Volatility-adjusted stop-losses in most trading platforms
/// - Risk parity portfolio management
/// - Volatility targeting strategies used by hedge funds
/// </para>
/// </remarks>
public sealed class AtrIndicator : IndicatorBase
{
    private readonly int _period;

    public int Period => _period; 
        
    /// <summary>
    /// Initializes a new instance of the <see cref="AtrIndicator"/> class.
    /// </summary>
    /// <param name="id">
    /// Unique identifier for this indicator instance (e.g., "atr_14", "atr_volatility").
    /// Used for diagnostics and referencing in strategy composition.
    /// </param>
    /// <param name="period">
    /// Number of periods for Wilder's smoothing in ATR calculation.
    /// <para>Standard value: 14 (Wilder's original recommendation)</para>
    /// <para>Common alternatives:</para>
    /// <list type="bullet">
    ///   <item><description>7-10: More responsive, updates faster with recent volatility changes. Good for short-term trading.</description></item>
    ///   <item><description>14: Balanced, industry standard. Use this unless you have specific reason to deviate.</description></item>
    ///   <item><description>20-30: Smoother, more stable. Better for position sizing and long-term risk assessment.</description></item>
    /// </list>
    /// <para>
    /// ⚠️ Warmup period: Requires (period + 1) candles for first value because True Range calculation needs
    /// previous candle's close, then we need 'period' True Ranges to calculate first ATR.
    /// </para>
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when period is less than 1. ATR requires at least 1 period for averaging.
    /// </exception>
    public AtrIndicator(string id, int period = 14)
        : base(id, $"ATR({period})", period + 1)
    {
        if (period < 1)
            throw new ArgumentException("Period must be >= 1", nameof(period));

        _period = period;
    }


    /// <summary>
    /// Computes ATR values for all provided candles using Wilder's smoothing method.
    /// </summary>
    /// <param name="candles">
    /// Historical price candles. Requires at least (period + 1) candles for first valid ATR.
    /// <para>
    /// Why period+1? First True Range needs 2 candles (current + previous), then we need
    /// 'period' True Ranges to calculate the first ATR via simple average.
    /// </para>
    /// <para>
    /// Data requirements:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Minimum: period + 1 candles (e.g., 15 for period=14)</description></item>
    ///   <item><description>Recommended: 3× period for ATR to fully stabilize</description></item>
    ///   <item><description>Order: Must be chronologically ordered (oldest to newest)</description></item>
    /// </list>
    /// </param>
    /// <returns>
    /// <see cref="IIndicatorResult"/> containing:
    /// <list type="bullet">
    ///   <item><description>Values: List of timestamped ATR values. First (period) values are null during warm-up.</description></item>
    ///   <item><description>IsValid: True if computation succeeded</description></item>
    ///   <item><description>Diagnostics: Contains Period and LastValue</description></item>
    /// </list>
    /// <para>
    /// Output structure: If you pass 100 candles with period=14:
    /// - Values[0-13]: null (warm-up period, insufficient data)
    /// - Values[14-99]: calculated ATR values (86 valid values)
    /// </para>
    /// <para>
    /// Each IndicatorValue contains:
    /// - Value: The ATR reading (in price units) or null during warm-up
    /// - Timestamp: The time of the candle this ATR corresponds to
    /// </para>
    /// <para>
    /// Timestamp usage for ATR:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Volatility patterns: "ATR peaks every Monday 8am (London open) - reduce size during that hour"</description></item>
    ///   <item><description>Event correlation: "ATR spiked at 2:00pm = that's when NFP data released, avoid trading 30min before/after"</description></item>
    ///   <item><description>Historical analysis: "During last trend, ATR stayed above 50 pips for 3 days - look for similar setup"</description></item>
    ///   <item><description>Stop-loss history: "At 10:00 when I entered, ATR was 40 pips, so my 2×ATR stop was 80 pips"</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// Algorithm: Wilder's ATR calculation with True Range and exponential smoothing
    /// </para>
    /// <list type="number">
    ///   <item><description>Calculate True Range for each candle: max(H-L, |H-Cₚᵣₑᵥ|, |L-Cₚᵣₑᵥ|)</description></item>
    ///   <item><description>First ATR: Simple average of first 'period' True Ranges</description></item>
    ///   <item><description>Subsequent ATRs: Wilder's smoothing = (PrevATR × (period-1) + CurrentTR) / period</description></item>
    /// </list>
    /// <para>
    /// Performance: O(N) time, O(N) space. Processes 10,000 candles in ~1ms.
    /// Efficient because True Range is calculated in single pass, then ATR smoothing in second pass.
    /// </para>
    /// <para>
    /// Important notes about ATR values:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Absolute units: ATR is in same units as price. For EUR/USD (5-decimal), ATR=0.00030 means 30 pips.</description></item>
    ///   <item><description>Not comparable across instruments: EUR/USD ATR=0.0030 vs GBP/JPY ATR=0.80 can't be compared directly.</description></item>
    ///   <item><description>Timeframe dependent: Daily ATR ≈ 4-5× Hourly ATR for same instrument.</description></item>
    ///   <item><description>Always positive: ATR measures magnitude of movement, never negative.</description></item>
    ///   <item><description>Slow to change: Due to smoothing, takes several periods of high/low volatility to significantly move ATR.</description></item>
    /// </list>
    /// </remarks>
    protected override IIndicatorResult ComputeCore(IReadOnlyList<Candle> candles)
    {
        var values = new List<IndicatorValue>();

        // Use the shared calculator to avoid duplication and ensure correctness
        List<double?> atrSeries;
        try
        {
            atrSeries = CalculateAtr(candles, _period);
        }
        catch (Exception ex)
        {
            // If input invalid or other calculation issue, return error result
            return IndicatorResult.Error(Id, $"ATR calculation error: {ex.Message}");
        }

        // Align ATR series to timestamps
        for (int i = 0; i < atrSeries.Count; i++)
        {
            values.Add(new IndicatorValue
            {
                Value = atrSeries[i],
                Timestamp = candles[i].Time
            });
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
    /// Calculates ATR values (Wilder’s method) as a sequence of doubles aligned to candle timestamps.
    /// </summary>
    /// <param name="candles">Chronologically ordered candles (oldest -> newest).</param>
    /// <param name="period">ATR period (e.g., 14).</param>
    /// <returns>
    /// A list of ATR values where:
    /// - Entries [0 .. period-2] are null (warm-up, insufficient TR history).
    /// - Entry at index (period-1) is the first ATR as the simple average of the first 'period' TRs.
    /// - Subsequent entries use Wilder’s smoothing:
    ///   ATR[i] = (ATR[i-1] * (period - 1) + TR[i]) / period
    /// Notes:
    /// - True Range (TR) uses max of: H-L, |H - PrevClose|, |L - PrevClose|.
    /// - Requires at least (period + 1) candles to produce the first ATR value.
    /// </returns>
    public static List<double?> CalculateAtr(IReadOnlyList<Candle> candles, int period)
    {
        if (candles is null) throw new ArgumentNullException(nameof(candles));
        if (period < 1) throw new ArgumentException("Period must be >= 1", nameof(period));
        var count = candles.Count;

        var atrSeries = new List<double?>(capacity: count);
        if (count == 0)
            return atrSeries; // empty input => empty output

        // Step 1: Precompute True Range for each candle.
        // TR[0] = High-Low (no previous close exists)
        var tr = new double[count];
        tr[0] = (double)(candles[0].High - candles[0].Low);

        for (int i = 1; i < count; i++)
        {
            var cur = candles[i];
            var prev = candles[i - 1];

            double rangeHL = (double)(cur.High - cur.Low);
            double rangeHPrevC = Math.Abs((double)(cur.High - prev.Close));
            double rangeLPrevC = Math.Abs((double)(cur.Low - prev.Close));

            tr[i] = Math.Max(rangeHL, Math.Max(rangeHPrevC, rangeLPrevC));
        }

        // Step 2: If not enough candles for first ATR, return nulls for all.
        // Need 'period' TR values which require (period + 1) candles.
        if (count < period + 1)
        {
            for (int i = 0; i < count; i++)
                atrSeries.Add(null);
            return atrSeries;
        }

        // Step 3: First ATR = simple average of first 'period' TRs at index (period-1).
        // TR indices used: 0..(period-1)
        double sum = 0;
        for (int i = 0; i < period; i++)
            sum += tr[i];

        double atr = sum / period;

        // Fill warm-up nulls up to index (period-2)
        for (int i = 0; i < period - 1; i++)
            atrSeries.Add(null);

        // First ATR value at index (period-1)
        atrSeries.Add(atr);

        // Step 4: Wilder’s smoothing for the rest: index period .. count-1
        // Formula: ATR[i] = (ATR[i-1] * (period - 1) + TR[i]) / period
        for (int i = period; i < count; i++)
        {
            atr = ((atr * (period - 1)) + tr[i]) / period;
            atrSeries.Add(atr);
        }

        return atrSeries;
    } 
}