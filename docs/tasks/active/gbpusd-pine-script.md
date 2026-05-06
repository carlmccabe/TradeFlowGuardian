# Task: GBP/USD Pine Script

**Branch:** `develop`
**BACKLOG status to set:** `active` when you start, `done` when merged
**Scope:** Pine Script file only — no C# changes, no API changes, no dashboard changes

---

## What to build

Create `pine/gbpusd_emac_signal.pine` — a TradingView Pine Script indicator for
GBP/USD that sends correctly structured webhook alerts to the TradeFlow Guardian API.

The script is identical in structure to `pine/usdjpy_emac_signal.pine` with these
differences:
- Instrument: `GBP_USD` (not `USD_JPY`)
- ATR Stop multiplier: `2.5` (not `2.6`)
- ATR Target multiplier: `2.9` (not `5.3`)
- Price precision: `0.#####` (5dp — same format string; GBP/USD is 5dp)
- Idempotency key prefix: `GBP_USD_`

---

## Context

Read `CLAUDE.md` and `docs/tasks/AGENT_INSTRUCTIONS.md` first.

### Template to copy and adapt

`pine/usdjpy_emac_signal.pine` is the reference implementation. Copy it, change the
five values listed above, update the header comments. Do not change the signal logic,
alert structure, or JSON field names.

### Risk parameters (from CLAUDE.md)

| Pair | ATR Stop | ATR Target |
|---|---|---|
| USD_JPY | 2.6× | 5.3× |
| EUR_USD | 2.4× | 4.6× |
| **GBP_USD** | **2.5×** | **2.9×** |

### JSON payload field names (unchanged)

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

- [ ] `pine/gbpusd_emac_signal.pine` exists
- [ ] `var string INSTRUMENT = "GBP_USD"`
- [ ] `atrStopMul` default value is `2.5`
- [ ] `atrTgtMul` default value is `2.9`
- [ ] Indicator title is `"TradeFlow Guardian — GBP/USD"`
- [ ] All three alert payloads (Close, Long, Short) use `instrument = "GBP_USD"` via the INSTRUMENT variable
- [ ] Idempotency keys are `GBP_USD_{time}_C`, `GBP_USD_{time}_L`, `GBP_USD_{time}_S`
- [ ] Price format string is `"0.#####"` (5dp)
- [ ] Header comment updated to reflect GBP/USD and correct multipliers
- [ ] Alert message box instruction comment is present (leave blank)
- [ ] No changes to any C# files, no changes to any other `.pine` files

---

## Out of scope

- Changing signal logic or EMA lengths
- Adding GBP/USD-specific filters
- Any backend or dashboard changes
- Modifying `usdjpy_emac_signal.pine` or `eurusd_emac_signal.pine`

---

## Gotchas

- Note that GBP/USD has a **tight target** (2.9× ATR) compared to USD_JPY (5.3×).
  This is correct per CLAUDE.md — do not "fix" it.
- GBP/USD is volatile — the ATR can be large. The `0.#####` format handles this.
- Do not hardcode `"GBP_USD"` in alert format strings — use the `INSTRUMENT` variable.
