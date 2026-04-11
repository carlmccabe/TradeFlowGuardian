# TradeFlow Guardian — Claude Code Context

## What This Is
API-native forex trade execution engine replacing MT4 EA + VPS.
Receives TradingView webhook signals → applies filters → executes via OANDA v20 REST API.

## Architecture
```
TradingView Alert (webhook POST)
        ↓
TradeFlowGuardian.Api          ← ASP.NET Core 10, receives + queues signals
        ↓ (Channel<TradeSignal>)
TradeFlowGuardian.Worker       ← .NET Worker Service, filters + executes
        ↓
OANDA v20 REST API             ← fxpractice (dev) / fxtrade (live)
```

## Project Structure
| Project | Role |
|---|---|
| `TradeFlowGuardian.Core` | Models, interfaces, config — zero dependencies |
| `TradeFlowGuardian.Infrastructure` | OandaClient, filters, queue — depends on Core only |
| `TradeFlowGuardian.Api` | Webhook receiver, status/kill-switch endpoints |
| `TradeFlowGuardian.Worker` | Background execution loop, signal handler |

## Conventions
- Target framework: **net10.0** across all projects
- Nullable enabled, implicit usings enabled
- No direct dependencies between Api and Worker — they share only via Core interfaces
- ISignalQueue is singleton — the bridge between Api (writer) and Worker (reader)
- Scoped services per signal in Worker (new DI scope per HandleAsync call)
- All OANDA calls go through IOandaClient — never call HttpClient directly from handlers
- Config via IOptions<T> pattern — never read IConfiguration directly in services
- Secrets via macOS Keychain (ACL-protected) for Docker dev — see [docs/SECRETS.md](./docs/SECRETS.md)
- Production secrets via Azure Key Vault only — live credentials never touch the dev machine
- Never commit secrets — no appsettings.Production.json, no .env files, no secrets.json

## Key Design Decisions
- **No pyramiding** — Worker checks for open position before every entry
- **Idempotency** — TradeSignal.IdempotencyKey prevents duplicate execution
- **Filters run in Worker, not Api** — Api just validates and queues; Worker decides
- **In-memory Channel queue** — Phase 1 only; Phase 2 replaces with Redis Streams
- **HMAC validation** — POST /api/signal only; GET endpoints are unauthenticated
- **Fallback FX rates** in PositionSizer — Phase 2 replaces with live OANDA pricing endpoint

## Current Phase: 1 — Foundation
### Done
- [x] Solution scaffold, all 4 projects
- [x] Core models: TradeSignal, TradeResult, FilterResult
- [x] Core interfaces: IOandaClient, ISignalQueue, ISignalFilter, IPositionSizer
- [x] OandaClient — market orders, close position, balance, open position query
- [x] PositionSizer — mirrors Pine Script Section 5 risk formula
- [x] InMemorySignalQueue — Channel<TradeSignal> bounded queue
- [x] Filters — SignalAgeFilter, AtrSpikeFilter, CompositeSignalFilter
- [x] SignalController — POST /api/signal (HMAC validated)
- [x] StatusController — balance, position query, emergency close
- [x] HmacValidationMiddleware — POST /api/signal only
- [x] Dockerfiles for Api and Worker
- [x] docker-compose for local dev
- [x] Migrated to .NET 10 LTS

### Next Up — Phase 2
- [x] Live FX rate feed in PositionSizer (replace hardcoded fallbacks)
- [x] Redis Streams queue (replace in-memory Channel)
- [x] Redis position state cache (replace in-process HashSet)
- [x] News calendar filter (ForexFactory or Finnhub)
- [x] Daily drawdown circuit breaker
- [ ] PostgreSQL trade history (schema + repository)

### Future — Phase 3
- [ ] SignalR hub for real-time P&L push
- [ ] React PWA dashboard (positions, P&L, kill switch, filter status)

- ## Phase 3 — Dashboard (React PWA)
- [x] Vite + React 18 + TypeScript scaffold (`TradeFlowGuardian.Dashboard/`)
- [x] Tailwind CSS (v4 via @tailwindcss/vite)
- [x] Account balance widget (polls /api/status/balance every 10s)
- [x] Open positions panel (per instrument, polls every 5s)
- [x] Filter status indicators (ATR spike, news, paused)
- [x] Kill switch button per instrument (POST /api/status/close/{instrument})
- [x] Global pause toggle (header, POST /api/status/pause)
- [x] Mobile-first layout (max-w-2xl, single column)
- [x] PWA manifest + meta tags
- [ ] P&L chart (daily, weekly)
- [ ] SignalR for real-time push (Phase 3 final)

### Phase 4 — Infrastructure
- [x] Railway deployment — Api live with public webhook URL
- [x] Railway deployment — Worker consuming from shared Redis
- [ ] GitHub Actions CI/CD
- [ ] Cloudflare DNS + SSL

## OANDA API Reference
- Base URL (practice): `https://api-fxpractice.oanda.com`
- Base URL (live): `https://api-fxtrade.oanda.com`
- Auth: `Authorization: Bearer {apiKey}` header
- Instrument format: `USD_JPY`, `EUR_USD`, `GBP_USD` (underscore, not slash)
- Units: positive = long, negative = short
- Price precision: 5dp for non-JPY pairs, 3dp for JPY pairs
- Docs: https://developer.oanda.com/rest-live-v20/introduction/

## TradingView Webhook Payload (expected JSON)
```json
{
  "instrument": "USD_JPY",
  "direction": "Long",
  "atr": 0.245,
  "price": 149.500,
  "riskPercent": 0,
  "timestamp": "2026-04-10T00:00:00Z",
  "idempotencyKey": "USDJPY_1744329600"
}
```
Direction values: `"Long"` | `"Short"` | `"Close"`

## Risk Parameters (current live settings)
| Pair | Risk % | ATR Stop | ATR Target | Magic (legacy MT4) |
|---|---|---|---|---|
| USD_JPY | 2.5% | 2.6× | 5.3× | 12348 |
| EUR_USD | 2.5% | 2.4× | 4.6× | 20250204 |
| GBP_USD | 2.5% | 2.5× | 2.9× | 12349 |

## What NOT To Do
- Do not add features mid-phase without principal engineer approval
- Do not read IConfiguration directly — always IOptions<T>
- Do not call OANDA API outside of OandaClient
- Do not add packages without checking .NET 10 compatibility first
- Do not modify appsettings.json with real credentials — use user-secrets
- Do not pyramid — one position per instrument, enforced in SignalExecutionHandler
- Do not skip the idempotency check

## Tech Debt
See [docs/TECH_DEBT.md](./docs/TECH_DEBT.md) for known issues and resolutions.

**High-priority items:**
- No open high-priority items.

When fixing bugs or implementing features, check docs/TECH_DEBT.md first to avoid duplicating known issues.

## Session Log
Add to the session log when you finish working on a new feature.

See [docs/SESSION_LOG.md](./docs/SESSION_LOG.md) for the format.