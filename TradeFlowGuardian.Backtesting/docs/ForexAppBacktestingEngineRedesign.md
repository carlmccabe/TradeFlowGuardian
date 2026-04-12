# Backtesting Engine - Technical Redesign Plan

## Current State Analysis

### Strengths to Preserve

1. **OANDA Integration** (`OandaHistoricalProvider`) - Good API chunking and error handling
2. **Strategy Architecture** - Clean separation of signals, filters, risk management
3. **Database Schema** - Working EF Core setup with proper migrations
4. **Console Commands** - Intuitive CLI interface

### Critical Issues to Address

1. **Data Quality Gaps** - No spread tracking, minimal gap validation
2. **Strategy Validation** - No systematic testing of strategy logic
3. **Performance Analysis** - Basic metrics only, no walk-forward
4. **Test Coverage** - No automated testing framework
5. **Market Context** - Missing pip sizes, trading sessions, spreads

## Phase 1: Data Foundation Rebuild

### 1.1 Enhanced Market Context Storage

```csharp
 // New entity for market context
 public class InstrumentContext 
 {
     public string Instrument { get; set; }
     public decimal PipSize { get; set; }
     public decimal PipValuePerUnit { get; set; }
     public decimal TypicalSpread { get; set; }
     public DateTime LastUpdated { get; set; }
 }
 
// Enhanced candle data with spread info
public class HistoricalCandle {
    // Existing properties... 
    public decimal BidOpen { get; set; }
    public decimal BidHigh { get; set; }
    public decimal BidLow { get; set; }
    public decimal BidClose { get; set; }
    public decimal AskOpen { get; set; }
    public decimal AskHigh { get; set; }
    public decimal AskLow { get; set; }
    public decimal AskClose { get; set; }
    public decimal Spread { get; set; } }
```

### 1.2 Data Validation Framework

```csharp
 public interface IDataValidator 
 {
     Task ValidateAsync(string instrument, string timeframe, DateTime start, DateTime end);
     Task DetectGapsAsync(string instrument, string timeframe, DateTime start, DateTime end);
     Task IsDataReliableAsync(string instrument, string timeframe, DateTime start, DateTime end, decimal minCoveragePercent = 80m);
 }  
 
public class DataValidationResult {
    public bool IsValid { get; set; }
    public decimal CoveragePercent { get; set; }
    public ListGaps { get; set; }
    public List Inconsistencies { get; set; }
    public MarketContextSummary Context { get; set; }
}
```

### 1.3 Enhanced Historical Data Provider

```csharp
    public Interface IHistoricalDataProvider
    {
        // Existing methods... 
        // New methods for robustness 
        Task<DataValidationResult> ValidateDataAsync(string instrument, string timeframe, DateTime start, DateTime end); 
        Task<MarketContext> GetMarketContextAsync(string instrument);
        Task<List<BacktestCandle>> GetValidatedDataAsync(string instrument, string timeframe, DateTime start, DateTime end, decimal minCoveragePercent = 80m); 
        Task FillDataGapsAsync(string instrument, string timeframe, List<DataGap> gaps);
    }
```

## Phase 2: Strategy Engine Rebuild

### 2.1 Strategy Validation Framework

```csharp
public interface IStrategyValidator
{
    TaskValidateAsync(IStrategy strategy, string instrument, DateTime start, DateTime end);
    Task IsStrategyDeterministicAsync(IStrategy strategy, List testData);
    Task AnalyzeParameterSensitivityAsync( IStrategy strategy, Dictionary  parameterRanges);
}  

public class StrategyValidationResult 
{
    public bool IsValid { get; set; }
    public bool IsDeterministic { get; set; }
    public ListIssues { get; set; }
    public PerformanceMetrics BaselinePerformance { get; set; }
    public RiskMetrics RiskProfile { get; set; }
}

```

### 2.2 Enhanced Execution Simulation

```csharp
public class ExecutionSimulator 
{
    private readonly MarketContext _context;
    
    public TradeExecution SimulateExecution(TradeSignal signal, BacktestCandle candle, decimal accountBalance)
    {
        // Realistic spread modeling
        var spread = CalculateRealisticSpread(candle, _context);
        
        // Slippage simulation
        var slippage = CalculateSlippage(signal.Size, candle.Volume);
        
        // Execution price with spread and slippage
        var executionPrice = CalculateExecutionPrice(signal, candle, spread, slippage);
        
        return new TradeExecution
        {
            RequestedPrice = signal.Price,
            ExecutedPrice = executionPrice,
            Spread = spread,
            Slippage = slippage,
            Commission = CalculateCommission(signal.Size, _context)
        };
    }
}

```

### 2.3 Enhanced Risk Management

```csharp
public Interface IRiskManager
{
    // Existing Methods... 
    
    // Enhanced methods 
    Task<PositionSizeResult> CalculateOptimalPositionSizeAsync(string instrument, decimal accountBalance, decimal riskPercent, decimal stopLossPips, MarketContext context);
    
    Task<RiskAssessment> AssessTradeRiskAsync(TradeSignal signal, decimal accountBalance, List<Position> openPositions);
    
    bool IsTradeWithinRiskLimits(TradeSignal signal, decimal accountBalance, RiskParameters riskParams);
}

public class RiskAssessment 
{
    public decimal RiskAmount { get; set; }
    public decimal RiskPercent { get; set; }
    public decimal PositionSize { get; set; }
    public bool IsAcceptable { get; set; }
    public ListWarnings { get; set; }
}


```

## Phase 3: Walk-Forward Analysis Implementation

### 3.1 Walk-Forward Engine

```csharp
public interface IWalkForwardEngine
{
    Task<WalkForwardResult> RunAsync(WalkForwardConfig config);
    Task<OptimizationResult> OptimizeStrategyAsync(IStrategy strategy, 
        OptimizationConfig config);
}

public class WalkForwardConfig
{
    public string Instrument { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan TrainingWindow { get; set; }
    public TimeSpan TestingWindow { get; set; }
    public TimeSpan StepSize { get; set; }
    public IStrategy BaseStrategy { get; set; }
    public Dictionary<string, object[]> ParameterRanges { get; set; }
}

public class WalkForwardResult
{
    public List<WalkForwardPeriod> Periods { get; set; }
    public PerformanceMetrics OverallPerformance { get; set; }
    public ParameterStabilityAnalysis Stability { get; set; }
    public OutOfSamplePerformance OutOfSample { get; set; }
}
```

### 3.2 Enhanced Performance Analysis

```csharp
public interface IPerformanceAnalyzer
{
    // Existing methods...

    // Enhanced analysis
    Task<ComprehensivePerformanceReport> AnalyzeAsync(BacktestResult result);
    Task<StrategyComparisonReport> CompareStrategiesAsync(List<BacktestResult> results);
    Task<RiskMetrics> CalculateRiskMetricsAsync(List<Trade> trades, 
        List<decimal> equityCurve);
    Task<DrawdownAnalysis> AnalyzeDrawdownsAsync(List<decimal> equityCurve);
}

public class ComprehensivePerformanceReport
{
public PerformanceMetrics Performance { get; set; }
public RiskMetrics Risk { get; set; }
public DrawdownAnalysis Drawdown { get; set; }
public TradeAnalysis Trades { get; set; }
public MonthlyReturns MonthlyBreakdown { get; set; }
public List<PerformanceWarning> Warnings { get; set; }
}
```

## Phase 4: Testing Framework

### 4.1 Unit Test Structure

```csharp
tests/
├── TradeFlowGuardian.Backtesting.Tests/
│   ├── Data/
│   │   ├── HistoricalDataProviderTests.cs
│   │   ├── DataValidatorTests.cs
│   │   └── MarketContextTests.cs
│   ├── Engine/
│   │   ├── BacktestEngineTests.cs
│   │   ├── ExecutionSimulatorTests.cs
│   │   └── WalkForwardEngineTests.cs
│   ├── Strategies/
│   │   ├── StrategyValidatorTests.cs
│   │   └── RiskManagerTests.cs
│   └── TestData/
│       ├── sample_candles.json
│       └── market_context.json
```

### 4.2 Test Data Management

```csharp
public class TestDataBuilder
{
    public static List<BacktestCandle> CreateTrendingData(int count, decimal startPrice,
        decimal trendStrength, DateTime startTime);
    
    public static List<BacktestCandle> CreateRangingData(int count, decimal centerPrice,
        decimal rangeSize, DateTime startTime);
    
    public static List<BacktestCandle> CreateVolatileData(int count, decimal startPrice,
        decimal volatility, DateTime startTime);
}
```

## Implementation Timeline

### Week 1-2: Data Foundation

- Add InstrumentContext entity and migration
- Enhance HistoricalCandle with bid/ask/spread data
- Implement DataValidator
- Update OandaHistoricalProvider
- Add data validation commands to console

### Week 3-4: Strategy Engine

- Implement StrategyValidator
- Build ExecutionSimulator
- Enhance RiskManager
- Add strategy validation commands
- Create strategy testing framework

### Week 5-6: Walk-Forward & Analysis

- Implement WalkForwardEngine
- Build comprehensive PerformanceAnalyzer
- Add comparison and ranking capabilities
- Create detailed reporting system

### Week 7-8: Testing & Polish

- Complete unit test coverage (80%+)
- Integration tests for full pipelines
- Performance testing and optimization
- Documentation and examples

## Success Criteria

### Data Quality

- 95%+ data coverage for major pairs
- Automatic spread and market context collection
- Sub-1% data inconsistency rate
- Real-time validation and alerting

### Strategy Reliability

- 100% deterministic strategy results
- Comprehensive validation framework
- Edge case handling verified
- Parameter sensitivity analysis

### Performance

- Full backtest (<2 minutes for 1 year of M5 data)
- Walk-forward analysis capability
- Memory efficient (handles multi-year datasets)
- Concurrent strategy testing

### Testing

- 80%+ code coverage
- Automated CI/CD pipeline
- Performance regression testing
- Documentation coverage

## Migration Strategy

### Phase 1: Parallel Development
- Keep existing system running
- Build new components alongside
- Test with subset of data

### Phase 2: Gradual Migration
- Migrate commands one by one
- Validate results against old system
- Preserve historical results

### Phase 3: Full Cutover
- Switch to new system
- Archive old codebase
- Update all documentation