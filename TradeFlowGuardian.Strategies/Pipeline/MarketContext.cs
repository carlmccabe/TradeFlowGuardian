using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Domain.Entities.Strategies.Core;

namespace TradeFlowGuardian.Strategies.Pipeline;

public sealed class MarketContext : IMarketContext
{
    public MarketContext(
        DateTime timestampUtc,
        string instrument,
        decimal accountBalance,
        decimal availableMargin,
        Position? openPosition,
        IReadOnlyList<Candle> candles,
        IReadOnlyDictionary<string, IIndicatorResult> indicators,
        string? correlationId = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        TimestampUtc = timestampUtc;
        Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
        AccountBalance = accountBalance;
        AvailableMargin = availableMargin;
        OpenPosition = openPosition;
        Candles = candles ?? throw new ArgumentNullException(nameof(candles));
        Indicators = indicators ?? throw new ArgumentNullException(nameof(indicators));
        CorrelationId = correlationId ?? Guid.NewGuid().ToString();
        Metadata = metadata ?? new Dictionary<string, object>();
    }

    public DateTime TimestampUtc { get; }
    public string Instrument { get; }
    public decimal AccountBalance { get; }
    public decimal AvailableMargin { get; }
    public Position? OpenPosition { get; }
    public IReadOnlyList<Candle> Candles { get; }
    public IReadOnlyDictionary<string, IIndicatorResult> Indicators { get; }
    public string CorrelationId { get; }
    public IReadOnlyDictionary<string, object> Metadata { get; }
}