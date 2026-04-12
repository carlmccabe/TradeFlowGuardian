// using TradeFlowGuardian.Domain.Entities;
// using TradeFlowGuardian.Domain.Entities.Strategies.Core;
//
// namespace TradeFlowGuardian.Strategies.Builder;
//
// public sealed class ComposableStrategy : IStrategy
// {
//     public string Name { get; }
//     
//     private readonly List<ISignalGenerator> _signalGenerators;
//     private readonly List<IFilter> _filters;
//     private readonly IRiskCalculator _riskCalculator;
//     private readonly IExitStrategy? _exitStrategy;
//
//     internal ComposableStrategy(
//         string name,
//         List<ISignalGenerator> signals,
//         List<IFilter> filters,
//         IRiskCalculator riskCalculator,
//         IExitStrategy? exitStrategy = null)
//     {
//         Name = name;
//         _signalGenerators = signals;
//         _filters = filters;
//         _riskCalculator = riskCalculator;
//         _exitStrategy = exitStrategy;
//     }
//
//     public Decision Evaluate(IReadOnlyList<Candle> m5Candles, DateTime nowUtc, bool hasOpenPosition, bool isLongPosition)
//     {
//         var context = new MarketContext
//         {
//             Candles = m5Candles,
//             CurrentTime = nowUtc,
//             HasOpenPosition = hasOpenPosition,
//             IsLongPosition = isLongPosition
//         };
//
//         // Check for exit conditions first
//         if (hasOpenPosition && _exitStrategy != null)
//         {
//             var exitDecision = _exitStrategy.ShouldExit(context);
//             if (exitDecision.Action == TradeAction.Exit)
//                 return new Decision(TradeAction.Exit, Reason: exitDecision.Reason);
//         }
//
//         // Skip entry signals if already in position
//         if (hasOpenPosition)
//             return new Decision(TradeAction.Hold, Reason: "Position already open");
//
//         // Generate signals from all generators
//         var signals = _signalGenerators
//             .Select(generator => generator.GenerateSignal(context))
//             .Where(signal => signal.Action != TradeAction.Hold)
//             .ToList();
//
//         if (!signals.Any())
//             return new Decision(TradeAction.Hold, Reason: "No signals generated");
//
//         // Find the strongest signal
//         var strongestSignal = signals.OrderByDescending(s => s.Confidence).First();
//
//         // Apply filters
//         foreach (var filter in _filters)
//         {
//             if (!filter.ShouldAllow(context, strongestSignal))
//                 return new Decision(TradeAction.Hold, Reason: $"Filtered by {filter.Name}");
//         }
//
//         // Calculate risk parameters
//         var riskParams = _riskCalculator.CalculateRisk(context, strongestSignal);
//
//         return new Decision(
//             strongestSignal.Action,
//             riskParams.StopLoss,
//             riskParams.TakeProfit,
//             strongestSignal.Reason
//         );
//     }
// }
//
// public interface IExitStrategy
// {
//     SignalResult ShouldExit(MarketContext context);
// }
//
// public sealed class StrategyBuilder
// {
//     private readonly List<ISignalGenerator> _signals = new();
//     private readonly List<IFilter> _filters = new();
//     private IRiskCalculator? _riskCalculator;
//     private IExitStrategy? _exitStrategy;
//     private string _name = "ComposableStrategy";
//
//     public StrategyBuilder WithName(string name)
//     {
//         _name = name;
//         return this;
//     }
//
//     public StrategyBuilder AddSignal(ISignalGenerator signal)
//     {
//         _signals.Add(signal);
//         return this;
//     }
//
//     public StrategyBuilder AddFilter(IFilter filter)
//     {
//         _filters.Add(filter);
//         return this;
//     }
//
//     public StrategyBuilder WithRiskCalculator(IRiskCalculator riskCalculator)
//     {
//         _riskCalculator = riskCalculator;
//         return this;
//     }
//
//     public StrategyBuilder WithExitStrategy(IExitStrategy exitStrategy)
//     {
//         _exitStrategy = exitStrategy;
//         return this;
//     }
//
//     public IStrategy Build()
//     {
//         if (_signals.Count == 0)
//             throw new InvalidOperationException("At least one signal generator is required");
//         
//         if (_riskCalculator == null)
//             throw new InvalidOperationException("Risk calculator is required");
//
//         return new ComposableStrategy(_name, _signals, _filters, _riskCalculator!, _exitStrategy);
//     }
// }
