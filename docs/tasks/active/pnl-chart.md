# Task: P&L Chart (daily/weekly) in React Dashboard

**Branch:** `develop`
**BACKLOG status to set:** `active` when you start, `done` when merged

---

## What to build

A P&L chart panel in the React dashboard showing realized profit/loss over time,
sourced from the `trade_history` PostgreSQL table. Two views: daily bars and
weekly bars. The chart renders only filled trades (`success = true`).

---

## Context

Read `CLAUDE.md` and `docs/tasks/AGENT_INSTRUCTIONS.md` first.

### Backend — new API endpoint

Add `GET /api/status/pnl?range=daily|weekly` to `StatusController`.

Query the `trade_history` table grouped by UTC day (or week). Return an array of:
```json
[
  { "date": "2026-04-11", "pnl": 42.50, "tradeCount": 3 },
  { "date": "2026-04-12", "pnl": -18.20, "tradeCount": 2 }
]
```

P&L per trade = `fill_price - entry_price` × `units` (sign already correct for long/short
because `units` is positive for long and negative for short in the DB, and `entry_price`
is always the signal bar price). Only include rows where `success = true` and
`direction IN ('Long', 'Short')` (exclude Close rows — they have entry_price = 0).

SQL for daily grouping:
```sql
SELECT
    DATE_TRUNC('day', executed_at) AS day,
    SUM((fill_price - entry_price) * units)::float AS pnl,
    COUNT(*)                                        AS trade_count
FROM trade_history
WHERE success = true
  AND direction IN ('Long', 'Short')
  AND executed_at >= NOW() - INTERVAL '30 days'
GROUP BY 1
ORDER BY 1 ASC;
```

For weekly: replace `'day'` with `'week'` and `'30 days'` with `'90 days'`.

The endpoint should accept `?range=daily` (default, 30 days) or `?range=weekly` (90 days).

### Frontend — new component

Add `PnlChart.tsx` to `TradeFlowGuardian.Dashboard/src/components/`.
Use **Recharts** (add `recharts` to package.json — check .NET 10 compatibility is N/A
since this is npm). Bar chart with date on X-axis, P&L (AUD) on Y-axis.
Green bars for positive, red for negative.

Add a daily/weekly toggle (two buttons, styled like Tailwind pill buttons).

Mount it in `App.tsx` between `BalanceWidget` and `PositionsPanel`.

---

## Files to read before starting

| File | Why |
|---|---|
| `TradeFlowGuardian.Api/Controllers/StatusController.cs` | Add endpoint here |
| `TradeFlowGuardian.Core/Interfaces/IServices.cs` | `ITradeHistoryRepository` interface |
| `TradeFlowGuardian.Infrastructure/History/TradeHistoryRepository.cs` | Add new query method |
| `TradeFlowGuardian.Dashboard/src/api/client.ts` | Add `getPnl()` call here |
| `TradeFlowGuardian.Dashboard/src/App.tsx` | Mount chart here |
| `TradeFlowGuardian.Dashboard/src/components/BalanceWidget.tsx` | Pattern to follow |
| `docs/migrations/001_trade_history.sql` | Full trade_history schema |

---

## Acceptance criteria

- [ ] `GET /api/status/pnl?range=daily` returns JSON array with `date`, `pnl`, `tradeCount`
- [ ] `GET /api/status/pnl?range=weekly` returns same shape, weekly buckets
- [ ] Empty array returned (not error) when trade_history has no matching rows
- [ ] New method added to `ITradeHistoryRepository` and implemented in `TradeHistoryRepository`
- [ ] `PnlChart.tsx` renders a bar chart with Recharts
- [ ] Bars are green for pnl > 0, red for pnl ≤ 0
- [ ] Daily/weekly toggle works
- [ ] Chart polls every 60 s (not 5 s — it's historical data)
- [ ] Chart handles empty data gracefully (shows "No trade history yet")
- [ ] `npm run build` succeeds with no TypeScript errors
- [ ] `dotnet build TradeFlowGuardian.sln` clean (0 errors)
- [ ] Existing tests still pass

---

## Out of scope

- Unrealized P&L (open positions) — that's shown in PositionsPanel already
- Per-instrument breakdown
- CSV export
- Equity curve (cumulative P&L line) — separate future task
- Any backend changes to how trade history is written

---

## Gotchas

- `ITradeHistoryRepository` is in `TradeFlowGuardian.Core/Interfaces/IServices.cs` —
  add the new method signature there, implement in Infrastructure, inject in StatusController.
  StatusController already has `ITradeHistoryRepository` injected (constructor parameter).
- `TradeHistoryRepository` uses Dapper with raw SQL — add a new method using the same
  pattern as `GetStatusAsync`. The P&L SQL above is already correct.
- Recharts needs `@types/recharts` — check if it's bundled or needs a separate install.
  As of Recharts v2.x, types are included. Use `recharts@^2.x` not v3 (API changed).
- `entry_price` can be 0 for Close-direction rows — the WHERE clause already filters those.
- The `units` column is `BIGINT` in Postgres and will come back as a number in C#.
  Cast in SQL to avoid decimal precision issues: `(fill_price - entry_price) * units`.
