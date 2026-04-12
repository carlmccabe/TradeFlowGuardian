# Backtest Engine — Usage Guide

Strategy backtesting runs entirely through the existing `TradeFlowGuardian.Api`.
Historical candles are fetched from OANDA on first run and cached in PostgreSQL,
so repeat runs over the same date range are fast.

---

## Prerequisites

### 1. Run the migration

```bash
psql $DATABASE_URL -f docs/migrations/002_backtest_tables.sql
```

This creates four tables: `HistoricalCandles`, `BacktestRuns`, `BacktestTrades`,
`BacktestEquityCurve`. Safe to re-run — all statements use `IF NOT EXISTS`.

### 2. Confirm config

The backtest engine shares the existing `Oanda` and `Postgres` config sections —
no new environment variables are required.

```json
// appsettings.json (already present)
"Oanda": {
  "ApiKey":      "...",
  "AccountId":   "...",
  "Environment": "fxpractice"   // or "fxtrade"
},
"Postgres": {
  "ConnectionString": "Host=...;Database=tradeflow;Username=...;Password=..."
}
```

---

## API endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/backtest/run` | Run a backtest and save the result |
| `GET`  | `/api/backtest/runs` | List saved runs (summary, newest first) |
| `GET`  | `/api/backtest/runs/{id}` | Load a saved run with full trades + equity curve |
| `GET`  | `/api/backtest/strategies` | List available strategy preset names |

---

## Running a backtest

### Request body (`POST /api/backtest/run`)

```json
{
  "name":           "EUR/USD H1 — 2024 full year",
  "instrument":     "EUR_USD",
  "timeframe":      "H1",
  "startDate":      "2024-01-01T00:00:00Z",
  "endDate":        "2024-12-31T00:00:00Z",
  "strategyPreset": "emac_10_30",
  "initialBalance": 10000,
  "riskPerTrade":   0.01,
  "commission":     7,
  "spreadPips":     0.5
}
```

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `name` | string | required | Label for this run |
| `instrument` | string | `"EUR_USD"` | OANDA format (`EUR_USD`, `USD_JPY`, `GBP_USD`) |
| `timeframe` | string | `"H1"` | `M1` `M5` `M15` `M30` `H1` `H4` `D` |
| `startDate` | ISO 8601 | required | UTC |
| `endDate` | ISO 8601 | required | UTC |
| `strategyPreset` | string | `"emac_10_30"` | See presets table below |
| `fastPeriods` | int? | null | Only used with `emac_custom` |
| `slowPeriods` | int? | null | Only used with `emac_custom` |
| `initialBalance` | decimal | `10000` | USD |
| `riskPerTrade` | decimal | `0.01` | Fraction of balance (0.01 = 1%) |
| `commission` | decimal | `7` | USD per 100k lot (round-trip) |
| `spreadPips` | decimal | `0.5` | Added to entry cost |

### Response

```json
{
  "id":            "3fa85f64-...",
  "name":          "EUR/USD H1 — 2024 full year",
  "strategyName":  "EMAC 10/30",
  "instrument":    "EUR_USD",
  "timeframe":     "H1",
  "startDate":     "2024-01-01T00:00:00Z",
  "endDate":       "2024-12-31T00:00:00Z",
  "initialBalance": 10000.00,
  "finalBalance":   11423.56,
  "totalReturn":    0.142356,
  "duration":       "00:01:42.3",
  "metrics": {
    "totalTrades":    47,
    "winningTrades":  29,
    "losingTrades":   18,
    "winRate":        0.617,
    "profitFactor":   1.84,
    "maxDrawdown":    0.063,
    "sharpeRatio":    1.21,
    "sortinoRatio":   1.87,
    "calmarRatio":    2.25,
    "averageWin":     183.40,
    "averageLoss":    -99.60,
    "largestWin":     521.00,
    "largestLoss":    -187.00
  },
  "trades":      [ ... ],
  "equityCurve": [ ... ]
}
```

---

## Strategy presets

| Preset | Description | Best for |
|--------|-------------|----------|
| `emac_10_30` | EMA 10/30 crossover | Intraday / H1 |
| `emac_9_21` | EMA 9/21 crossover — faster entries | M15 / H1 |
| `emac_12_26` | EMA 12/26 (MACD-style) | H1 / H4 |
| `emac_custom` | Any EMA periods via `fastPeriods` + `slowPeriods` | Ad-hoc optimisation |

All presets use `FilteredSignalRule` with min-confidence 0.5. No session time
filters are applied by default — add them by extending `StrategyFactory`.

### Custom EMA example

```json
{
  "name":           "EUR/USD H4 — EMA 20/50 2024",
  "instrument":     "EUR_USD",
  "timeframe":      "H4",
  "startDate":      "2024-01-01T00:00:00Z",
  "endDate":        "2024-12-31T00:00:00Z",
  "strategyPreset": "emac_custom",
  "fastPeriods":    20,
  "slowPeriods":    50
}
```

---

## cURL examples

### Run a backtest

```bash
curl -s -X POST https://<your-api>/api/backtest/run \
  -H "Content-Type: application/json" \
  -d '{
    "name": "USDJPY H1 2024",
    "instrument": "USD_JPY",
    "timeframe": "H1",
    "startDate": "2024-01-01T00:00:00Z",
    "endDate": "2024-12-31T00:00:00Z",
    "strategyPreset": "emac_10_30",
    "riskPerTrade": 0.025
  }' | jq '.metrics'
```

### List recent runs

```bash
curl -s https://<your-api>/api/backtest/runs?limit=10 | jq '.[] | {name, instrument, totalReturn, sharpeRatio}'
```

### Load a saved run

```bash
curl -s https://<your-api>/api/backtest/runs/3fa85f64-... | jq '{totalTrades: .metrics.totalTrades, trades: (.trades | length)}'
```

### List available strategies

```bash
curl -s https://<your-api>/api/backtest/strategies
# ["emac_10_30","emac_9_21","emac_12_26","emac_custom"]
```

---

## Performance notes

- **First run** over a new instrument/timeframe/date range fetches candles from OANDA
  in 5 000-candle chunks with a 500 ms inter-chunk delay. A full year of H1 data
  (~6 000 bars) takes ~30–60 s depending on network.
- **Subsequent runs** over the same range load from `HistoricalCandles` and complete
  in 1–5 s.
- Backtests run synchronously on the API request thread. For large date ranges
  consider setting a long client timeout (120 s+).

---

## Adding new strategies

Open `TradeFlowGuardian.Backtesting/Strategies/StrategyFactory.cs` and add a new
case to the switch expression:

```csharp
"emac_adx" => BuildEmacWithAdx("emac_adx", 10, 30, adxThreshold: 25),
```

Then add the corresponding private builder method. The `BacktestController` and
`StrategyFactory.SupportedPresets` will automatically reflect the new preset —
no controller changes needed.

---

## Architecture

```
POST /api/backtest/run
        │
BacktestController
        │  resolves preset via StrategyFactory
        │  builds BacktestRequest(IStrategy, ...)
        ▼
IBacktestEngine.RunBacktestAsync()
        │
        ├─ IHistoricalDataProvider.GetHistoricalDataAsync()
        │       └─ checks HistoricalCandles (PostgreSQL)
        │          fetches gaps from IOandaApiService (OANDA /candles)
        │          caches new candles back to PostgreSQL
        │
        └─ per-bar loop:
               IStrategy.Evaluate(candles, nowUtc, hasPosition, isLong)
                   └─ PipelineStrategy
                          └─ IPipeline.Execute(candles, accountState, nowUtc)
                                 └─ EmaCrossoverSignal → FilteredSignalRule → Decision

        SaveBacktestResultAsync() → BacktestRuns / BacktestTrades / BacktestEquityCurve
```
