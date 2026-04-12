using TradeFlowGuardian.Domain.Entities;

namespace TradeFlowGuardian.Strategies.Indicators;

public static class BasicIndicators
{
    public static decimal Sma(IReadOnlyList<decimal> series, int n)
        => series.Count >= n ? series.Skip(series.Count - n).Average() : 0m;

    public static decimal Ema(IReadOnlyList<decimal> series, int n)
    {
        if (series.Count == 0 || n <= 1) return series.LastOrDefault();
        var k = 2m / (n + 1);
        decimal ema = series[0];
        for (int i = 1; i < series.Count; i++)
            ema = series[i] * k + ema * (1 - k);
        return ema;
    }

    public static List<decimal> EmaSeq(IReadOnlyList<decimal> series, int n)
    {
        var result = new List<decimal>(series.Count);
        if (series.Count == 0) return result;
        var k = 2m / (n + 1);
        decimal ema = series[0];
        result.Add(ema);
        for (int i = 1; i < series.Count; i++)
        {
            ema = series[i] * k + ema * (1 - k);
            result.Add(ema);
        }
        return result;
    }

    public static decimal RelativeStrengthIndex(IReadOnlyList<decimal> closes, int n = 14)
    {
        if (closes.Count <= n) return 50m;
        
        decimal gain = 0, loss = 0;
        for (int i = closes.Count - n + 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change >= 0) gain += change; else loss += -change;
        }
        if (loss == 0) return 100m;
        
        var rs = (gain / n) / (loss / n);
        return 100m - (100m / (1 + rs));
    }

    public static decimal Atr(IReadOnlyList<Candle> candles, int n = 14)
    {
        if (candles.Count < n + 1) return 0m;
        var trs = new List<decimal>();
        for (int i = candles.Count - n; i < candles.Count; i++)
        {
            var c = candles[i];
            var prev = candles[i - 1];
            var tr = new[] { (c.High - c.Low), Math.Abs(c.High - prev.Close), Math.Abs(c.Low - prev.Close) }.Max();
            trs.Add(tr);
        }
        return trs.Average();
    }

    public static decimal Highest(IReadOnlyList<decimal> series, int n)
        => series.Count >= n ? series.Skip(series.Count - n).Max() : series.DefaultIfEmpty(0m).Max();

    public static decimal Lowest(IReadOnlyList<decimal> series, int n)
        => series.Count >= n ? series.Skip(series.Count - n).Min() : series.DefaultIfEmpty(0m).Min();
}
