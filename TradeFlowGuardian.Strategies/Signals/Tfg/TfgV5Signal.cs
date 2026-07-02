using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Signals.Base;

namespace TradeFlowGuardian.Strategies.Signals.Tfg;

/// <summary>
/// C# port of the TFG v5 Pine entry logic (pine/TFG Live &amp; Strategies/TFG_USDJPY_live.pine):
///
///   long  = SMA(fast) crosses above SMA(slow)
///           AND close &gt; EMA(trend)  AND RSI &gt; 50
///           AND ATR rising  AND EMA distance within [min, max] pips
///   short = mirror image
///   SL = close ∓ slMult × ATR,  TP = close ± tpMult × ATR
///
/// The session gate lives in a separate TimeFilter (see StrategyFactory) so it stays
/// visible in pipeline traces. Indicators are computed over a bounded trailing window
/// (the tail weight beyond it is negligible) because the backtest engine re-evaluates
/// on every bar with a growing candle list.
/// </summary>
public sealed class TfgV5Signal : SignalBase
{
    private readonly int _smaFast;
    private readonly int _smaSlow;
    private readonly int _emaPeriod;
    private readonly int _rsiPeriod;
    private readonly int _atrPeriod;
    private readonly decimal _slMult;
    private readonly decimal _tpMult;
    private readonly decimal _emaDistMinPips;
    private readonly decimal _emaDistMaxPips;
    private readonly decimal _pipSize;

    public TfgV5Signal(
        string id,
        int smaFast = 9,
        int smaSlow = 25,
        int emaPeriod = 179,
        int rsiPeriod = 18,
        int atrPeriod = 13,
        decimal slMult = 2.6m,
        decimal tpMult = 5.3m,
        decimal emaDistMinPips = 5.0m,
        decimal emaDistMaxPips = 69.0m,
        decimal pipSize = 0.01m)
        : base(id, $"TFGv5({smaFast}/{smaSlow},EMA{emaPeriod},RSI{rsiPeriod},ATR{atrPeriod},SL{slMult}x,TP{tpMult}x)")
    {
        if (smaSlow <= smaFast)
            throw new ArgumentException("Slow SMA period must exceed fast SMA period", nameof(smaSlow));
        if (slMult <= 0 || tpMult <= 0)
            throw new ArgumentException("SL/TP multipliers must be positive");

        _smaFast        = smaFast;
        _smaSlow        = smaSlow;
        _emaPeriod      = emaPeriod;
        _rsiPeriod      = rsiPeriod;
        _atrPeriod      = atrPeriod;
        _slMult         = slMult;
        _tpMult         = tpMult;
        _emaDistMinPips = emaDistMinPips;
        _emaDistMaxPips = emaDistMaxPips;
        _pipSize        = pipSize;
    }

    protected override SignalResult GenerateCore(IMarketContext context)
    {
        var candles = context.Candles;
        var required = Math.Max(Math.Max(_smaSlow + 1, _emaPeriod + 1), Math.Max(_rsiPeriod + 2, _atrPeriod + 2));
        if (candles.Count < required)
            return NeutralResult($"Insufficient data: need {required}, have {candles.Count}", context.TimestampUtc);

        var close = candles[^1].Close;

        // ── Indicators (Pine ta.sma / ta.ema / ta.rsi / ta.atr equivalents) ──
        var fastNow  = Sma(candles, _smaFast, 0);
        var slowNow  = Sma(candles, _smaSlow, 0);
        var fastPrev = Sma(candles, _smaFast, 1);
        var slowPrev = Sma(candles, _smaSlow, 1);

        var ema = Ema(candles, _emaPeriod);
        var rsi = Rsi(candles, _rsiPeriod);
        var (atr, atrPrev) = Atr(candles, _atrPeriod);

        var emaDistPips = Math.Abs(close - ema) / _pipSize;

        // ── Gates (identical to Pine longOk/shortOk) ──────────────────────────
        var crossUp   = fastPrev <= slowPrev && fastNow > slowNow;
        var crossDown = fastPrev >= slowPrev && fastNow < slowNow;
        var atrRising = atr > atrPrev;
        var distOk    = emaDistPips >= _emaDistMinPips && emaDistPips <= _emaDistMaxPips;

        var diagnostics = new Dictionary<string, object>
        {
            ["FastSma"] = fastNow, ["SlowSma"] = slowNow,
            ["Ema"] = ema, ["Rsi"] = rsi,
            ["Atr"] = atr, ["AtrPrev"] = atrPrev,
            ["EmaDistPips"] = emaDistPips,
            ["CrossUp"] = crossUp, ["CrossDown"] = crossDown,
            ["AtrRising"] = atrRising, ["DistOk"] = distOk
        };

        if (!crossUp && !crossDown)
            return Neutral("No SMA crossover", context.TimestampUtc, diagnostics);

        var wantLong = crossUp;
        string? blocked =
            wantLong && close <= ema        ? "close below trend EMA" :
            !wantLong && close >= ema       ? "close above trend EMA" :
            wantLong && rsi <= 50m          ? $"RSI {rsi:F1} not above 50" :
            !wantLong && rsi >= 50m         ? $"RSI {rsi:F1} not below 50" :
            !atrRising                      ? "ATR not rising" :
            !distOk                         ? $"EMA distance {emaDistPips:F1} pips outside [{_emaDistMinPips}, {_emaDistMaxPips}]" :
            null;

        if (blocked != null)
            return Neutral($"{(wantLong ? "Long" : "Short")} cross blocked: {blocked}", context.TimestampUtc, diagnostics);

        var stopLoss   = wantLong ? close - _slMult * atr : close + _slMult * atr;
        var takeProfit = wantLong ? close + _tpMult * atr : close - _tpMult * atr;

        return new SignalResult
        {
            Direction = wantLong ? SignalDirection.Long : SignalDirection.Short,
            Confidence = 1.0,
            Reason = $"TFGv5 {(wantLong ? "long" : "short")}: SMA({_smaFast}/{_smaSlow}) cross, " +
                     $"RSI={rsi:F1}, dist={emaDistPips:F1}p, ATR={atr:F5} rising | SL={stopLoss:F3} TP={takeProfit:F3}",
            SuggestedStopLoss = stopLoss,
            SuggestedTakeProfit = takeProfit,
            GeneratedAt = context.TimestampUtc,
            Diagnostics = diagnostics
        };
    }

    private static SignalResult Neutral(string reason, DateTime at, Dictionary<string, object> diagnostics) =>
        new()
        {
            Direction = SignalDirection.Neutral,
            Confidence = 0.0,
            Reason = reason,
            GeneratedAt = at,
            Diagnostics = diagnostics
        };

    // ── Indicator math ────────────────────────────────────────────────────────

    /// <summary>Simple moving average of closes, offset bars back from the latest.</summary>
    private static decimal Sma(IReadOnlyList<Candle> candles, int period, int offset)
    {
        var end = candles.Count - offset; // exclusive
        var sum = 0m;
        for (var i = end - period; i < end; i++)
            sum += candles[i].Close;
        return sum / period;
    }

    /// <summary>
    /// Pine ta.ema: SMA seed then α = 2/(n+1). Computed over a trailing window of
    /// 6×period — the ignored tail carries &lt; 0.1% of the weight.
    /// </summary>
    private static decimal Ema(IReadOnlyList<Candle> candles, int period)
    {
        var window = Math.Min(candles.Count, period * 6);
        var start = candles.Count - window;

        var seed = 0m;
        for (var i = start; i < start + period; i++)
            seed += candles[i].Close;
        var ema = seed / period;

        var alpha = 2m / (period + 1);
        for (var i = start + period; i < candles.Count; i++)
            ema += alpha * (candles[i].Close - ema);
        return ema;
    }

    /// <summary>
    /// Pine ta.rsi: Wilder smoothing (α = 1/n) of gains/losses, SMA seed.
    /// Trailing window of 12×period keeps the discarded tail weight negligible.
    /// </summary>
    private static decimal Rsi(IReadOnlyList<Candle> candles, int period)
    {
        var window = Math.Min(candles.Count - 1, period * 12); // number of price changes
        var firstChangeIdx = candles.Count - window;           // change at i uses closes[i-1] → closes[i]

        decimal avgGain = 0m, avgLoss = 0m;
        for (var i = firstChangeIdx; i < firstChangeIdx + period; i++)
        {
            var change = candles[i].Close - candles[i - 1].Close;
            if (change > 0) avgGain += change;
            else avgLoss -= change;
        }
        avgGain /= period;
        avgLoss /= period;

        for (var i = firstChangeIdx + period; i < candles.Count; i++)
        {
            var change = candles[i].Close - candles[i - 1].Close;
            avgGain = (avgGain * (period - 1) + Math.Max(change, 0m)) / period;
            avgLoss = (avgLoss * (period - 1) + Math.Max(-change, 0m)) / period;
        }

        if (avgLoss == 0m) return avgGain == 0m ? 50m : 100m;
        var rs = avgGain / avgLoss;
        return 100m - 100m / (1m + rs);
    }

    /// <summary>
    /// Pine ta.atr: Wilder smoothing of true range. Returns the current and previous
    /// values so the caller can evaluate the "ATR rising" gate.
    /// </summary>
    private static (decimal Atr, decimal AtrPrev) Atr(IReadOnlyList<Candle> candles, int period)
    {
        var window = Math.Min(candles.Count - 1, period * 12); // number of TR values
        var firstTrIdx = candles.Count - window;

        var seed = 0m;
        for (var i = firstTrIdx; i < firstTrIdx + period; i++)
            seed += TrueRange(candles, i);
        var atr = seed / period;
        var atrPrev = atr;

        for (var i = firstTrIdx + period; i < candles.Count; i++)
        {
            atrPrev = atr;
            atr = (atr * (period - 1) + TrueRange(candles, i)) / period;
        }
        return (atr, atrPrev);
    }

    private static decimal TrueRange(IReadOnlyList<Candle> candles, int i)
    {
        var prevClose = candles[i - 1].Close;
        var high = candles[i].High;
        var low = candles[i].Low;
        return Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));
    }
}
