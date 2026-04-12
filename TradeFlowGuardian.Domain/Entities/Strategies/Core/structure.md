Core/
│   ├── Interfaces/
│   │   ├── IMarketContext.cs
│   │   ├── IIndicator.cs
│   │   ├── IFilter.cs
│   │   ├── ISignal.cs
│   │   ├── IRule.cs
│   │   └── IPipeline.cs
│   ├── Models/
│   │   ├── FilterResult.cs
│   │   ├── SignalResult.cs
│   │   ├── RuleDecision.cs
│   │   ├── PipelineResult.cs
│   │   └── EvaluationTrace.cs
│   └── Enums/
│       ├── SignalDirection.cs
│       ├── TradeAction.cs
│       └── PriceSource.cs


TradeFlowGuardian/
├── src/
│   ├── TradeFlowGuardian.Domain/
│   │   └── Entities/
│   │       └── Strategies/
│   │           └── Core/
│   │               ├── IMarketContext.cs        [NEW]
│   │               ├── IIndicator.cs            [NEW]
│   │               ├── IFilter.cs               [EXTEND EXISTING]
│   │               ├── ISignal.cs               [EXTEND EXISTING]
│   │               ├── IRule.cs                 [EXTEND EXISTING]
│   │               └── IPipeline.cs             [EXTEND EXISTING]
│   │
│   ├── TradeFlowGuardian.Strategies/
│   │   ├── Indicators/
│   │   │   ├── Base/
│   │   │   │   └── IndicatorBase.cs             [NEW]
│   │   │   ├── SmaIndicator.cs                  [NEW]
│   │   │   ├── EmaIndicator.cs                  [NEW]
│   │   │   ├── RsiIndicator.cs                  [NEW]
│   │   │   └── AtrIndicator.cs                  [NEW]
│   │   │
│   │   ├── Filters/
│   │   │   ├── Base/
│   │   │   │   └── FilterBase.cs                [NEW]
│   │   │   ├── Composite/
│   │   │   │   ├── AndFilter.cs                 [NEW]
│   │   │   │   ├── OrFilter.cs                  [NEW]
│   │   │   │   └── NotFilter.cs                 [NEW]
│   │   │   ├── RsiThresholdFilter.cs            [NEW]
│   │   │   ├── TrendFilter.cs                   [NEW]
│   │   │   └── TimeFilter.cs                    [NEW]
│   │   │
│   │   ├── Signals/
│   │   │   ├── Base/
│   │   │   │   └── SignalBase.cs                [NEW]
│   │   │   └── CrossoverSignal.cs               [NEW]
│   │   │
│   │   ├── Rules/
│   │   │   └── FilteredSignalRule.cs            [NEW]
│   │   │
│   │   ├── Pipeline/
│   │   │   ├── MarketContext.cs                 [NEW]
│   │   │   └── StandardPipeline.cs              [NEW]
│   │   │
│   │   └── Builders/
│   │       └── PipelineBuilder.cs               [NEW]
│   │
│   └── TradeFlowGuardian.Backtesting/
│       ├── Engine/
│       │   └── BacktestEngine.cs                [EXISTING - will integrate]
│       └── Data/
│           └── OandaHistoricalProvider.cs       [EXISTING - will use]
│
└── tests/
└── TradeFlowGuardian.Strategies.Tests/
├── Shared/
│   ├── MockData.cs                       [NEW]
│   ├── TestFixtures.cs                   [NEW]
│   └── TestBase.cs                       [NEW]
├── Indicators/
│   ├── SmaIndicatorTests.cs              [NEW]
│   ├── EmaIndicatorTests.cs              [NEW]
│   ├── RsiIndicatorTests.cs              [NEW]
│   └── AtrIndicatorTests.cs              [NEW]
├── Filters/
│   ├── CompositeFilterTests.cs           [NEW]
│   ├── RsiThresholdFilterTests.cs        [NEW]
│   ├── TrendFilterTests.cs               [NEW]
│   └── TimeFilterTests.cs                [NEW]
├── Signals/
│   └── CrossoverSignalTests.cs           [NEW]
├── Rules/
│   └── FilteredSignalRuleTests.cs        [NEW]
└── Integration/
└── PipelineIntegrationTests.cs       [NEW]