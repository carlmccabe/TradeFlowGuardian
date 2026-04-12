
// File: src/TradeFlowGuardian.Strategies/Signals/Base/SignalBase.cs
using TradeFlowGuardian.Domain.Entities.Strategies.Core;

namespace TradeFlowGuardian.Strategies.Signals.Base
{
    public abstract class SignalBase : ISignal
    {
        protected SignalBase(string id, string signalType)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            SignalType = signalType ?? throw new ArgumentNullException(nameof(signalType));
        }

        public string Id { get; }
        public string SignalType { get; }

        public SignalResult Generate(IMarketContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                return GenerateCore(context);
            }
            catch (Exception ex)
            {
                return new SignalResult
                {
                    Direction = SignalDirection.Neutral,
                    Confidence = 0.0,
                    Reason = $"Signal error: {ex.Message}",
                    GeneratedAt = DateTime.UtcNow,
                    Diagnostics = new Dictionary<string, object>
                    {
                        ["Exception"] = ex.ToString()
                    }
                };
            }
        }

        protected abstract SignalResult GenerateCore(IMarketContext context);
    
        protected static SignalResult NeutralResult(string reason, DateTime timestamp)
        {
            return new SignalResult
            {
                Direction = SignalDirection.Neutral,
                Confidence = 0.0,
                Reason = reason,
                GeneratedAt = timestamp
            };
        }
    }
}

