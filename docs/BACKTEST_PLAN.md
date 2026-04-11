# Backtest Framework — Design Plan

## Overview

TradeFlowGuardian does not generate signals — it receives them from TradingView via webhook.
A backtest engine therefore needs two independent inputs:

1. **Historical signals** — what would TradingView have fired?
2. **Historical prices** — what would fills and P&L have been?

These are solved differently, and that split shapes the entire architecture.

---

## Signal Sources

### Phase 1 — TradingView CSV Upload

TradingView's Strategy Tester can export a trade list. The user adds output lines to
their Pine Script to include ATR + direction in the export, downloads the CSV, and
uploads it via the dashboard. This is low-effort and uses the exact same strategy logic
that fires live webhooks, so the backtest is faithful.

Required CSV columns (user adds to Pine Script):

```
timestamp, instrument, direction, atr, price, idempotencyKey
```

This maps directly to `TradeSignal` — no schema mismatch.

### Phase 2 — Replay from PostgreSQL

Once the Phase 2 trade history table is live, the backtest engine can replay real
historical webhooks the system actually received. No export step required.
Highest-fidelity approach.

---

## Price Data

**Primary source: OANDA v20 Candles API**

```
GET /v3/instruments/{instrument}/candles?granularity=H1&from=...&to=...
```

- Already authenticated — no new credentials needed
- Real bid/ask/mid OHLCV data, spreads included
- Years of history for major pairs (USD_JPY, EUR_USD, GBP_USD)
- H1 granularity returns ~5,000 candles per call, pageable

Only change needed: add `GetCandlesAsync(instrument, granularity, from, to)` to
`IOandaClient` and `OandaClient`.

### Alternatives Considered

| Source | Quality | Cost | Integration effort | Decision |
|---|---|---|---|---|
| OANDA v20 Candles | Excellent — real bid/ask | Free (practice account) | Low — already authenticated | **Use this** |
| Histdata.com / Dukascopy | Tick-level | Free | High — file downloads, custom parser | Not worth it |
| Yahoo Finance / Quandl | Poor FX quality | Free | Medium | Avoid |

### Historical News Events

ForexFactory's iCal feed is forward-looking only — no historical archive.

- **Phase 1**: Run backtests with the news filter disabled. Document it as an explicit
  assumption in the backtest result.
- **Phase 2**: Integrate Finnhub's economic calendar API. It provides historical
  high-impact events and the free tier covers the volume needed.

---

## Backend Architecture

### New Endpoints

```
POST /api/backtest/run
  Body: { instrument, from, to, signals: TradeSignal[], params: BacktestParams }
  Response: 202 Accepted + { id: string }

GET  /api/backtest/{id}
  Response: BacktestResult (or 202 while running)

GET  /api/backtest/{id}/trades
  Response: paginated BacktestTrade[]
```

### Execution Engine (`BacktestEngine` in Infrastructure)

For each signal in the uploaded list:

1. Fetch the H1 candle at `signal.Timestamp` from the OANDA candles cache
2. Run the existing filter chain:
   - `PauseFilter` — skipped (not meaningful in backtest context)
   - `AtrSpikeFilter` — uses `signal.Atr`, identical to live behaviour
   - `NewsCalendarFilter` — disabled in Phase 1; mocked in Phase 2 using Finnhub history
3. If allowed: call `PositionSizer.CalculateUnitsAsync` with the current simulated balance
4. Simulate fill at candle close (configurable — see assumptions below)
5. Calculate exit price using ATR-based SL/TP multiples from `RiskConfig`
6. Record `BacktestTrade`
7. Update simulated balance

### New Core Models

```csharp
// Core/Models/BacktestTrade.cs
public record BacktestTrade(
    DateTimeOffset EntryTime,
    DateTimeOffset ExitTime,
    string Instrument,
    string Direction,
    decimal EntryPrice,
    decimal ExitPrice,
    long Units,
    decimal Pnl,
    bool WasFiltered,
    string? FilterReason
);

// Core/Models/BacktestResult.cs
public record BacktestResult(
    string Id,
    string Instrument,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<BacktestTrade> Trades,
    IReadOnlyList<EquityPoint> EquityCurve,
    decimal TotalReturn,
    decimal SharpeRatio,
    decimal MaxDrawdown,
    decimal WinRate,
    decimal ProfitFactor,
    BacktestFilterStats FilterStats
);

public record EquityPoint(DateTimeOffset At, decimal Balance);
public record BacktestFilterStats(int AtrSpike, int NewsBlocked, int TooOld, int Executed);
```

The engine reuses the existing filter chain, `PositionSizer`, and `RiskConfig` — the only
genuinely new code is the candle fetch, fill simulator, and equity curve aggregation.

---

## Frontend Architecture

### New Route: `/backtest`

```
BacktestPage
├── BacktestConfigPanel
│     Instrument selector    (USD_JPY / EUR_USD / GBP_USD)
│     Date range picker      (from / to)
│     Starting balance       (defaults to current live balance)
│     Filter overrides       (toggle news filter, ATR spike multiplier)
│     Fill model toggle      (candle close vs. next candle open)
│
├── SignalSourcePanel
│     Phase 1: drag-and-drop CSV upload (TradingView Strategy Tester export)
│              signal preview table (first N rows, validate schema)
│     Phase 2: "Use saved signals" date range (reads from PostgreSQL)
│
└── BacktestResultsPanel  (rendered after run completes)
      EquityCurveChart       Recharts LineChart, balance over time
      MetricsSummary cards:
        Total Return | Sharpe | Max Drawdown | Win Rate | Profit Factor | # Trades
      FilterBreakdown:
        N signals blocked — ATR spike: X | News blackout: Y | Too old: Z
      TradesTable (paginated):
        Entry time | Direction | Entry | Exit | Units | P&L | Reason
```

**Chart library**: Recharts — lightweight, composable, works with the existing Tailwind
setup. The `EquityCurveChart` component can be shared with the Phase 3 live P&L chart.

---

## Execution Simulation Assumptions

These are the places where backtest P&L can diverge from live trading. All should be
surfaced in the results UI so the user knows what was simulated.

| Assumption | Phase 1 | Phase 2 |
|---|---|---|
| Fill price | Candle close of signal bar | Configurable: close or next open |
| Slippage | Zero | Modelled from bid/ask spread in candle data |
| News filter | Disabled | Finnhub historical events |
| ATR spike | Signal ATR used (faithful) | Same |
| Balance compounding | Fixed starting balance | Optional toggle |
| Partial fills | Not modelled | Not planned |

---

## Implementation Order

1. `IOandaClient.GetCandlesAsync` + `OandaClient` implementation
2. `BacktestTrade`, `BacktestResult`, `EquityPoint` models in Core
3. `BacktestEngine` in Infrastructure (filter chain + fill simulation + metrics)
4. `BacktestController` in Api
5. Frontend: `BacktestConfigPanel` + CSV upload
6. Frontend: `BacktestResultsPanel` + `EquityCurveChart`
7. (Phase 2) Finnhub news integration for historical filter replay
8. (Phase 2) PostgreSQL signal replay

---

## Open Questions

- **Pine Script ATR export**: The user's Pine Script strategy must be updated to include
  `atr` and `idempotencyKey` in the CSV export. Confirm format before building the parser.
- **Candle granularity**: Strategy runs on H1. Backtest should match. Confirm with user
  whether M15 resolution is also needed for more precise fill simulation.
- **Multi-instrument**: Phase 1 runs one instrument per backtest job. Phase 2 can
  support portfolio-level runs (all three pairs simultaneously, shared balance).
- **Result persistence**: Phase 1 results are in-memory (lost on restart). Phase 2
  persists to PostgreSQL alongside trade history.
