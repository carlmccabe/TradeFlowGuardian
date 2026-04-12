namespace TradeFlowGuardian.Domain.Entities;

/// <summary>
/// Represents a single price candlestick (also known as OHLC bar) in financial markets.
/// </summary>
/// <remarks>
/// <para>
/// A candlestick visualizes price movement during a specific time period (e.g., 1 minute, 5 minutes, 1 hour, 1 day).
/// It contains four critical price points plus volume information:
/// </para>
/// <list type="bullet">
///   <item><description><b>Open:</b> The first traded price when the period began</description></item>
///   <item><description><b>High:</b> The highest price reached during the period</description></item>
///   <item><description><b>Low:</b> The lowest price reached during the period</description></item>
///   <item><description><b>Close:</b> The last traded price when the period ended</description></item>
///   <item><description><b>Volume:</b> Total number of units traded during the period</description></item>
/// </list>
/// <para>
/// <b>Candlestick Anatomy:</b>
/// </para>
/// <para>
/// The "body" of the candle is the area between Open and Close. The "shadows" (or "wicks") are the lines
/// extending above and below the body representing the High and Low extremes:
/// </para>
/// <code>
///     High ─┬─  ← Upper Shadow (wick)
///          │
///     ┌────┴────┐
///     │  BODY   │  ← Body (Open to Close)
///     └────┬────┘
///          │
///     Low ──┴──  ← Lower Shadow (wick)
/// </code>
/// <para>
/// <b>Bullish vs Bearish Candles:</b>
/// </para>
/// <list type="bullet">
///   <item><description><b>Bullish (Green/White):</b> Close > Open (price went up). Buyers were in control.</description></item>
///   <item><description><b>Bearish (Red/Black):</b> Close &lt; Open (price went down). Sellers were in control.</description></item>
///   <item><description><b>Doji:</b> Close = Open (indecision). Neither buyers nor sellers dominated.</description></item>
/// </list>
/// <para>
/// <b>Technical Analysis Properties:</b>
/// </para>
/// <para>
/// This class provides computed properties for quick technical analysis:
/// </para>
/// <list type="bullet">
///   <item><description><b>BodySize:</b> Magnitude of price change (|Close - Open|). Large bodies indicate strong momentum.</description></item>
///   <item><description><b>UpperShadow:</b> Distance from body top to High. Long upper shadows suggest rejection at higher prices.</description></item>
///   <item><description><b>LowerShadow:</b> Distance from body bottom to Low. Long lower shadows suggest rejection at lower prices.</description></item>
///   <item><description><b>Range:</b> Total price movement (High - Low). Indicates volatility during the period.</description></item>
/// </list>
/// <para>
/// <b>Data Integrity Invariants:</b>
/// </para>
/// <para>
/// Valid candlestick data must satisfy:
/// </para>
/// <list type="number">
///   <item><description>High >= Max(Open, Close) - The high must be at or above the body</description></item>
///   <item><description>Low &lt;= Min(Open, Close) - The low must be at or below the body</description></item>
///   <item><description>High >= Low - The high cannot be lower than the low</description></item>
///   <item><description>All prices > 0 - No negative or zero prices in forex/stocks</description></item>
///   <item><description>Volume >= 0 - Volume cannot be negative (0 is acceptable for some data sources)</description></item>
/// </list>
/// <para>
/// Use <see cref="Validate"/> method to check data integrity.
/// </para>
/// <para>
/// <b>Common Candlestick Patterns:</b>
/// </para>
/// <list type="bullet">
///   <item><description><b>Hammer:</b> Small body, long lower shadow, little/no upper shadow (bullish reversal)</description></item>
///   <item><description><b>Shooting Star:</b> Small body, long upper shadow, little/no lower shadow (bearish reversal)</description></item>
///   <item><description><b>Engulfing:</b> Current candle's body completely engulfs previous body (strong reversal)</description></item>
///   <item><description><b>Doji:</b> Open = Close, represents indecision (potential reversal at extremes)</description></item>
///   <item><description><b>Marubozu:</b> No shadows, body = range (very strong directional move)</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Create a bullish candle (price went from 1.0850 to 1.0875)
/// var bullishCandle = new Candle(
///     time: DateTime.UtcNow,
///     open: 1.0850m,
///     high: 1.0880m,  // Reached 1.0880 at some point
///     low: 1.0845m,   // Dipped to 1.0845 at some point
///     close: 1.0875m, // Closed higher than open
///     volume: 15000
/// );
/// 
/// Console.WriteLine($"Direction: {(bullishCandle.IsBullish ? "Bullish" : "Bearish")}");
/// Console.WriteLine($"Body Size: {bullishCandle.BodySize} pips");
/// Console.WriteLine($"Range: {bullishCandle.Range} pips");
/// 
/// // Validate data integrity
/// var errors = bullishCandle.Validate();
/// if (errors.Any())
///     Console.WriteLine($"⚠️ Invalid candle: {string.Join(", ", errors)}");
/// </code>
/// </example>
public sealed class Candle
{
    /// <summary>
    /// Gets or sets the timestamp when this candlestick period began.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a 5-minute candle starting at 14:30:00, Time would be 14:30:00.
    /// The candle covers the period from 14:30:00 to 14:34:59.
    /// </para>
    /// <para>
    /// <b>Important:</b> Always use UTC for consistency across time zones, especially for
    /// 24-hour forex markets. Do NOT use local time unless explicitly required for display.
    /// </para>
    /// </remarks>
    public DateTime Time { get; set; }

    /// <summary>
    /// Gets or sets the opening price - the first traded price when the period began.
    /// </summary>
    /// <remarks>
    /// The Open is the baseline for determining if the candle is bullish (Close > Open)
    /// or bearish (Close &lt; Open). In low-volume periods or gaps, Open may not equal
    /// the previous candle's Close.
    /// </remarks>
    public decimal Open { get; set; }

    /// <summary>
    /// Gets or sets the highest price reached during the candlestick period.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The High represents maximum bullish pressure during the period. If High is significantly
    /// above the Close, it suggests that buyers pushed price up but couldn't sustain it,
    /// indicating potential resistance or profit-taking.
    /// </para>
    /// <para>
    /// <b>Invariant:</b> High must be >= Max(Open, Close, Low).
    /// </para>
    /// </remarks>
    public decimal High { get; set; }

    /// <summary>
    /// Gets or sets the lowest price reached during the candlestick period.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Low represents maximum bearish pressure during the period. If Low is significantly
    /// below the Close, it suggests that sellers pushed price down but couldn't sustain it,
    /// indicating potential support or buying interest.
    /// </para>
    /// <para>
    /// <b>Invariant:</b> Low must be &lt;= Min(Open, Close, High).
    /// </para>
    /// </remarks>
    public decimal Low { get; set; }

    /// <summary>
    /// Gets or sets the closing price - the last traded price when the period ended.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Close is generally considered the most important price point because it represents
    /// the final consensus of value for that period. Most technical indicators (moving averages,
    /// RSI, MACD) use closing prices.
    /// </para>
    /// <para>
    /// The relationship between Close and Open determines candle direction:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Close > Open = Bullish (green/white candle)</description></item>
    ///   <item><description>Close &lt; Open = Bearish (red/black candle)</description></item>
    ///   <item><description>Close = Open = Doji (indecision candle)</description></item>
    /// </list>
    /// </remarks>
    public decimal Close { get; set; }

    /// <summary>
    /// Gets or sets the trading volume - total number of units/contracts traded during the period.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Volume confirms the strength of price movements:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><b>High Volume + Large Body:</b> Strong conviction, trend likely to continue</description></item>
    ///   <item><description><b>High Volume + Small Body:</b> Accumulation/distribution, potential reversal</description></item>
    ///   <item><description><b>Low Volume + Large Body:</b> Weak move, likely false breakout</description></item>
    ///   <item><description><b>Low Volume + Small Body:</b> Consolidation, waiting for catalyst</description></item>
    /// </list>
    /// <para>
    /// <b>Note:</b> In spot forex markets, true volume data is often unavailable (decentralized market).
    /// Brokers may provide "tick volume" (number of price changes) as a proxy, which correlates with
    /// but doesn't equal actual volume. For stocks/futures, volume is actual contracts traded.
    /// </para>
    /// <para>
    /// Default: 0 (acceptable for data sources that don't provide volume).
    /// </para>
    /// </remarks>
    public long Volume { get; set; }

    /// <summary>
    /// Gets or sets the financial instrument identifier (e.g., "EUR_USD", "GBP_JPY", "AAPL").
    /// </summary>
    /// <remarks>
    /// Used to identify which market this candle belongs to. Essential when storing or processing
    /// multiple instruments simultaneously. Convention varies by broker (EUR_USD, EURUSD, EUR/USD).
    /// </remarks>
    public string Instrument { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time granularity (timeframe) of this candle (e.g., "M1", "M5", "H1", "D").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Common granularities:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><b>M1:</b> 1 minute (scalping)</description></item>
    ///   <item><description><b>M5:</b> 5 minutes (intraday)</description></item>
    ///   <item><description><b>M15:</b> 15 minutes (intraday)</description></item>
    ///   <item><description><b>H1:</b> 1 hour (swing trading)</description></item>
    ///   <item><description><b>H4:</b> 4 hours (swing trading)</description></item>
    ///   <item><description><b>D:</b> Daily (position trading)</description></item>
    ///   <item><description><b>W:</b> Weekly (long-term)</description></item>
    /// </list>
    /// </remarks>
    public string Granularity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this candle data was recorded or received (system timestamp).
    /// </summary>
    /// <remarks>
    /// Differs from <see cref="Time"/> which is the market time when the candle period began.
    /// Timestamp is when the data was captured/stored, useful for data freshness checks and debugging.
    /// </remarks>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Candle"/> class with default values.
    /// </summary>
    /// <remarks>
    /// Parameterless constructor for ORM/serialization frameworks. Produces an invalid candle
    /// (all zeros) that should be populated immediately. Consider using the parameterized
    /// constructor for explicit initialization.
    /// </remarks>
    public Candle()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Candle"/> class with specified OHLCV values.
    /// </summary>
    /// <param name="time">The timestamp when this candlestick period began (use UTC).</param>
    /// <param name="open">The opening price (first trade of the period).</param>
    /// <param name="high">The highest price reached during the period.</param>
    /// <param name="low">The lowest price reached during the period.</param>
    /// <param name="close">The closing price (last trade of the period).</param>
    /// <param name="volume">
    /// The trading volume during the period. Default is 0 if volume data is unavailable.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>Note:</b> This constructor does NOT validate data integrity. The caller is responsible for
    /// ensuring High >= Max(Open, Close) and Low &lt;= Min(Open, Close). Use <see cref="Validate"/>
    /// to check validity after construction if data source is untrusted.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var candle = new Candle(
    ///     time: DateTime.UtcNow,
    ///     open: 1.0850m,
    ///     high: 1.0880m,
    ///     low: 1.0845m,
    ///     close: 1.0875m,
    ///     volume: 15000
    /// );
    /// </code>
    /// </example>
    public Candle(DateTime time, decimal open, decimal high, decimal low, decimal close, long volume = 0)
    {
        Time = time;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    /// <summary>
    /// Gets the size of the candle body (absolute difference between Close and Open).
    /// </summary>
    /// <remarks>
    /// <para>
    /// BodySize represents the net price change during the period, regardless of direction.
    /// Larger bodies indicate stronger directional momentum and conviction.
    /// </para>
    /// <para>
    /// <b>Interpretation:</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description><b>Large body (> 70% of Range):</b> Strong trend, decisive move</description></item>
    ///   <item><description><b>Medium body (30-70% of Range):</b> Normal price action</description></item>
    ///   <item><description><b>Small body (&lt; 30% of Range):</b> Indecision, consolidation</description></item>
    ///   <item><description><b>No body (= 0):</b> Doji, maximum indecision</description></item>
    /// </list>
    /// <para>
    /// Formula: |Close - Open|
    /// </para>
    /// </remarks>
    public decimal BodySize => Math.Abs(Close - Open);

    /// <summary>
    /// Gets the length of the upper shadow (wick above the candle body).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The upper shadow represents the distance from the top of the body to the High.
    /// It shows price rejection at higher levels.
    /// </para>
    /// <para>
    /// <b>Interpretation:</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description><b>Long upper shadow:</b> Buyers pushed price up but sellers regained control (bearish sign)</description></item>
    ///   <item><description><b>No upper shadow:</b> Price closed at/near high (strong bullish if bullish candle)</description></item>
    ///   <item><description><b>Very long upper shadow + small body:</b> Shooting star pattern (bearish reversal)</description></item>
    /// </list>
    /// <para>
    /// Formula: High - Max(Open, Close)
    /// </para>
    /// </remarks>
    public decimal UpperShadow => High - Math.Max(Open, Close);

    /// <summary>
    /// Gets the length of the lower shadow (wick below the candle body).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lower shadow represents the distance from the bottom of the body to the Low.
    /// It shows price rejection at lower levels.
    /// </para>
    /// <para>
    /// <b>Interpretation:</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description><b>Long lower shadow:</b> Sellers pushed price down but buyers regained control (bullish sign)</description></item>
    ///   <item><description><b>No lower shadow:</b> Price closed at/near low (strong bearish if bearish candle)</description></item>
    ///   <item><description><b>Very long lower shadow + small body:</b> Hammer pattern (bullish reversal)</description></item>
    /// </list>
    /// <para>
    /// Formula: Min(Open, Close) - Low
    /// </para>
    /// </remarks>
    public decimal LowerShadow => Math.Min(Open, Close) - Low;

    /// <summary>
    /// Gets the total range of the candle (distance from High to Low).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Range represents the total price volatility during the period. Used to measure
    /// market activity and calculate indicators like Average True Range (ATR).
    /// </para>
    /// <para>
    /// <b>Interpretation:</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description><b>Large range:</b> High volatility, active trading, potential breakout</description></item>
    ///   <item><description><b>Small range:</b> Low volatility, consolidation, accumulation/distribution</description></item>
    ///   <item><description><b>Expanding ranges:</b> Increasing volatility, trend acceleration</description></item>
    ///   <item><description><b>Contracting ranges:</b> Decreasing volatility, potential breakout imminent</description></item>
    /// </list>
    /// <para>
    /// Formula: High - Low
    /// </para>
    /// </remarks>
    public decimal Range => High - Low;

    /// <summary>
    /// Gets a value indicating whether this is a bullish candle (Close > Open).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bullish candles indicate buying pressure and upward price movement. Typically displayed
    /// as green or white in charting software.
    /// </para>
    /// <para>
    /// <b>Note:</b> A bullish candle doesn't guarantee continued upward movement. Context matters
    /// (e.g., bullish candle in a downtrend might be just a pullback).
    /// </para>
    /// </remarks>
    public bool IsBullish => Close > Open;

    /// <summary>
    /// Gets a value indicating whether this is a bearish candle (Close &lt; Open).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bearish candles indicate selling pressure and downward price movement. Typically displayed
    /// as red or black in charting software.
    /// </para>
    /// <para>
    /// <b>Note:</b> A bearish candle doesn't guarantee continued downward movement. Context matters
    /// (e.g., bearish candle in an uptrend might be just a pullback).
    /// </para>
    /// </remarks>
    public bool IsBearish => Close < Open;

    /// <summary>
    /// Gets a value indicating whether this is a doji candle (Open = Close).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A doji represents market indecision where buyers and sellers are equally matched.
    /// The candle has no body, only shadows (wicks).
    /// </para>
    /// <para>
    /// <b>Types of Doji:</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description><b>Standard Doji:</b> Open = Close with equal shadows (pure indecision)</description></item>
    ///   <item><description><b>Long-Legged Doji:</b> Open = Close with very long shadows (high volatility, strong indecision)</description></item>
    ///   <item><description><b>Dragonfly Doji:</b> Open = Close = High (long lower shadow, no upper shadow, bullish)</description></item>
    ///   <item><description><b>Gravestone Doji:</b> Open = Close = Low (long upper shadow, no lower shadow, bearish)</description></item>
    /// </list>
    /// <para>
    /// <b>Significance:</b> Dojis at trend extremes often signal potential reversals. In strong trends,
    /// dojis may indicate temporary exhaustion before continuation.
    /// </para>
    /// <para>
    /// <b>Note:</b> Due to decimal precision, exact equality (Open == Close) might be rare in real data.
    /// Consider using a threshold (e.g., |Close - Open| &lt; 0.0001) for practical doji detection.
    /// </para>
    /// </remarks>
    public bool IsDoji => Open == Close;

    /// <summary>
    /// Validates the candle data for integrity and returns a list of validation errors.
    /// </summary>
    /// <returns>
    /// A list of validation error messages. Empty list if the candle is valid.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method checks for common data integrity issues:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>High must be >= Low</description></item>
    ///   <item><description>High must be >= Open</description></item>
    ///   <item><description>High must be >= Close</description></item>
    ///   <item><description>Low must be &lt;= Open</description></item>
    ///   <item><description>Low must be &lt;= Close</description></item>
    ///   <item><description>All prices must be > 0</description></item>
    ///   <item><description>Volume must be >= 0</description></item>
    /// </list>
    /// <para>
    /// <b>Usage:</b> Call this method after loading data from external sources (APIs, CSV files)
    /// to ensure data quality before backtesting or analysis.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var candle = LoadCandleFromApi();
    /// var errors = candle.Validate();
    /// 
    /// if (errors.Any())
    /// {
    ///     Console.WriteLine($"Invalid candle at {candle.Time}:");
    ///     foreach (var error in errors)
    ///         Console.WriteLine($"  - {error}");
    ///     return;
    /// }
    /// 
    /// // Proceed with valid candle
    /// ProcessCandle(candle);
    /// </code>
    /// </example>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (High < Low)
            errors.Add($"High ({High}) cannot be less than Low ({Low})");

        if (High < Open)
            errors.Add($"High ({High}) cannot be less than Open ({Open})");

        if (High < Close)
            errors.Add($"High ({High}) cannot be less than Close ({Close})");

        if (Low > Open)
            errors.Add($"Low ({Low}) cannot be greater than Open ({Open})");

        if (Low > Close)
            errors.Add($"Low ({Low}) cannot be greater than Close ({Close})");

        if (Open <= 0)
            errors.Add($"Open ({Open}) must be greater than 0");

        if (High <= 0)
            errors.Add($"High ({High}) must be greater than 0");

        if (Low <= 0)
            errors.Add($"Low ({Low}) must be greater than 0");

        if (Close <= 0)
            errors.Add($"Close ({Close}) must be greater than 0");

        if (Volume < 0)
            errors.Add($"Volume ({Volume}) cannot be negative");

        return errors;
    }

    /// <summary>
    /// Returns a concise string representation of the candle with OHLCV data.
    /// </summary>
    /// <returns>
    /// Formatted string: "YYYY-MM-DD HH:mm O:xxxxx H:xxxxx L:xxxxx C:xxxxx V:xxxxx"
    /// </returns>
    /// <remarks>
    /// Prices are formatted to 5 decimal places (standard for most forex pairs).
    /// Useful for logging, debugging, and console output.
    /// </remarks>
    /// <example>
    /// Example output: "2025-10-04 14:30 O:1.08500 H:1.08750 L:1.08450 C:1.08625 V:15000"
    /// </example>
    public override string ToString() =>
        $"{Time:yyyy-MM-dd HH:mm} O:{Open:F5} H:{High:F5} L:{Low:F5} C:{Close:F5} V:{Volume}";
}