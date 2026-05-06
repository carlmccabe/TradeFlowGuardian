# Task: EUR/USD Pine Script

**Branch:** `develop`
**BACKLOG status to set:** `active` when you start, `done` when merged
**Scope:** Pine Script file only — no C# changes, no API changes, no dashboard changes

---

## What to build

Create `pine/eurusd_emac_signal.pine` — a TradingView Pine Script indicator for
EUR/USD that sends correctly structured webhook alerts to the TradeFlow Guardian API.

The script is identical in structure to `pine/usdjpy_emac_signal.pine` with these
differences:
- Instrument: `EUR_USD` (not `USD_JPY`)
- ATR Stop multiplier: `2.4` (not `2.6`)
- ATR Target multiplier: `4.6` (not `5.3`)
- Price precision: `0.#####` (5dp — same format string as USD_JPY; EUR/USD is already 5dp)
- Idempotency key prefix: `EUR_USD_`

---

## Context

Read `CLAUDE.md` and `docs/tasks/AGENT_INSTRUCTIONS.md` first.

### Template to copy and adapt

`pine/usdjpy_emac_signal.pine` is the reference implementation. Copy it, change the
five values listed above, update the header comments accordingly. Do not change the
signal logic, alert structure, or JSON field names.

### Risk parameters (from CLAUDE.md)

| Pair | ATR Stop | ATR Target |
|---|---|---|
| USD_JPY | 2.6× | 5.3× |
| **EUR_USD** | **2.4×** | **4.6×** |
| GBP_USD | 2.5× | 2.9× |

### JSON payload field names (unchanged from USD_JPY)

The C# `TradeSignal` model field names are camelCase and do not change per instrument:
```
instrument, direction, price, atr, stopLoss, takeProfit, riskPercent, idempotencyKey
```

---

## Files to read before starting

| File | Why |
|---|---|
| `pine/usdjpy_emac_signal.pine` | Template — copy and adapt |
| `CLAUDE.md` → Risk Parameters table | Confirms ATR multipliers |
| `TradeFlowGuardian.Core/Models/TradeSignal.cs` | Confirms JSON field names |

---

## Acceptance criteria

- [ ] `pine/eurusd_emac_signal.pine` exists
- [ ] `var string INSTRUMENT = "EUR_USD"` (not USD_JPY)
- [ ] `atrStopMul` default value is `2.4`
- [ ] `atrTgtMul` default value is `4.6`
- [ ] Indicator title is `"TradeFlow Guardian — EUR/USD"`
- [ ] All three alert payloads (Close, Long, Short) use `instrument = "EUR_USD"` via the INSTRUMENT variable
- [ ] Idempotency keys are `EUR_USD_{time}_C`, `EUR_USD_{time}_L`, `EUR_USD_{time}_S`
- [ ] Price format string is `"0.#####"` (5dp — correct for EUR/USD)
- [ ] Header comment updated to reflect EUR/USD and correct multipliers
- [ ] Alert message box instruction comment is present (leave blank)
- [ ] No changes to any C# files, no changes to `usdjpy_emac_signal.pine`

---

## Out of scope

- Changing signal logic or EMA lengths
- Adding EUR/USD-specific filters
- Any backend or dashboard changes
- Modifying `usdjpy_emac_signal.pine`

---

## Gotchas

- EUR/USD uses 5dp price precision (`0.#####`) — same format string as USD_JPY already
  uses; no change needed there.
- The `str.tostring(close, "0.#####")` format ensures values like `1.08524` render
  correctly (leading zero before decimal is guaranteed by `0.`).
- Do not hardcode `"EUR_USD"` in the alert format strings — use the `INSTRUMENT`
  variable via `{0}` so the instrument name in the JSON comes from a single source.
