using TradeFlowGuardian.Domain.Entities.Strategies.Core;
using TradeFlowGuardian.Strategies.Signals.Base;

namespace TradeFlowGuardian.Strategies.Signals.MeanReversion;

public class RocSignal : SignalBase
{
    private readonly int _period;
    private readonly decimal _threshold;
    private readonly bool _inverse;
    
    public RocSignal(string id, string signalType) : base(id, signalType)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id cannot be null or empty", nameof(id));
        if (string.IsNullOrEmpty(signalType)) throw new ArgumentException("SignalType cannot be null or empty", nameof(signalType));
    }

    protected override SignalResult GenerateCore(IMarketContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrEmpty(Id)) throw new ArgumentException("Id cannot be null or empty", nameof(Id));
        if (string.IsNullOrEmpty(SignalType)) throw new ArgumentException("SignalType cannot be null or empty", nameof(SignalType));

       return NeutralResult($"Not implemented for {_period} period", context.TimestampUtc);
    }
}