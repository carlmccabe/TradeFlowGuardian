# Session Log — TradeFlow Guardian

## Format
```
### YYYY-MM-DD
- What was done
- Key decisions made
- Blockers or issues found

### Next session goals
- What to tackle next
```

---

### 2026-04-10
- Scaffolded solution, all 4 projects
- Migrated to .NET 10
- Build clean, 0 errors
- OANDA practice connected, balance endpoint confirmed
- HMAC middleware fixed (POST only)
- CLAUDE.md created

### 2026-04-10 (session 2)
- Added `GetMidPriceAsync` to `IOandaClient` and implemented in `OandaClient` via `GET /v3/accounts/{id}/pricing?instruments=`
- `PositionSizer.GetRateAsync` now calls live endpoint; falls back to hardcoded rates only if API unreachable
- docs/ folder created; TECH_DEBT.md and SESSION_LOG.md moved there
- CLAUDE.md updated to reference new doc paths
- Build clean, 0 errors

- `GET /api/status/price/{instrument}` added to StatusController — tested live, confirmed mid prices from OANDA
- Worker appsettings.json fixed — Oanda/Risk/Filters sections added

### 2026-04-11
- Redis Streams queue implemented — `RedisSignalQueue` replaces `InMemorySignalQueue` in both API and Worker
  - `XADD` in API, `XREADGROUP` + `XACK` in Worker with consumer group `workers`
  - `RedisConfig` added to Core, both `appsettings.json` files updated
  - Resolves the cross-process queue limitation (Phase 1 known tech debt)
- `JsonStringEnumConverter` added to API — fixes `"Long"/"Short"/"Close"` deserialization
- End-to-end webhook test working — `scripts/test-signal.sh` confirmed signal queued (202)
- Worker `appsettings.json` fully populated (was missing Oanda/Risk/Filters — high priority tech debt cleared)
- Grafana monitoring stack added
  - Prometheus scraping API (`/metrics` port 8080) and Worker (`/metrics` port 9091)
  - Loki + Promtail for Docker log aggregation (Docker socket, no code changes)
  - Grafana auto-provisioned with Prometheus + Loki datasources and TradeFlow dashboard
  - RedisInsight for Redis stream visibility
  - `docker-compose.yml` restructured with `core` and `monitoring` profiles
- `prometheus-net` instrumented across the stack
  - `TradeMetrics` static class in `Infrastructure.Observability`
  - Metrics: `signals_received`, `signals_filtered{reason}`, `orders_placed{outcome}`, `order_latency_seconds` histogram, `account_balance` gauge, `redis_queue_depth` gauge (XPENDING)
  - `FilterResult` given `Label` property to avoid high-cardinality Prometheus labels
  - `KestrelMetricServer` on port 9091 via `MetricServerHostedService` in Worker
- Dockerfiles fixed — base images updated from `aspnet:8.0`/`sdk:8.0` to `aspnet:10.0`/`sdk:10.0`
- Docker Compose Watch configured — `develop.watch` on API and Worker for auto-rebuild on source changes
- Dev workflow standardised around Docker
  - `scripts/setup-secrets.sh` — one-time Keychain setup with ACL (`-T ""`) requiring system dialog on every read
  - `scripts/dev.sh` — daily driver pulling secrets from Keychain, runs `docker compose up --build --watch`
  - `launch.json` updated — Docker configs are now primary, `dotnet run` retained as local fallback
- `docs/SECRETS.md` created — threat model, Keychain ACL, 1Password CLI, Azure Key Vault, secret names reference
- CLAUDE.md updated to reference Keychain ACL + Azure Key Vault strategy

### 2026-04-11 (session 2)
- Cleared TECH_DEBT.md — Redis Streams confirmed in place, Dockerfiles confirmed on .NET 10 base images
- Redis position state cache implemented
  - `IPositionCache` interface added to Core
  - `RedisPositionCache` in `Infrastructure/Cache/` — keys `tradeflow:position:{instrument}`, 5-min TTL
  - `SignalExecutionHandler` updated: cache-first position check → OANDA fallback on miss; write-through on order placed; clear on close
  - `IPositionCache` registered as singleton in Worker `Program.cs`
  - All 5 unit tests passing
- Dashboard scaffolded — `TradeFlowGuardian.Dashboard/`
  - Vite 8 + React 18 + TypeScript + Tailwind CSS v4 via `@tailwindcss/vite`
  - PWA manifest (`/public/manifest.json`), meta tags, dark theme (`#030712`)
  - `api/client.ts` — typed fetch wrapper for all `/api/status/*` endpoints
  - `hooks/usePolling.ts` — generic interval polling hook (pre-SignalR bridge)
  - `BalanceWidget` — polls `/api/status/balance` every 10s; confirmed live: **$10,665.78 AUD**
  - `PositionsPanel` — polls `/api/status/positions` every 5s, per-instrument close button
  - `FilterStatus` — ATR spike / news / paused indicators
  - `PauseToggle` — global pause/resume in sticky header
  - Mobile-first layout (`max-w-2xl`, single column)
  - Vite dev proxy → `http://localhost:5205` (matches API launch profile)
- Fixed dashboard crash — `balanceAud` field name in API response didn't match TypeScript interface (`balance`); corrected, page now renders with live data

### 2026-04-11 (session 3)
- ForexFactory iCal economic calendar filter implemented
  - `EconomicEvent` record + `ImpactLevel` enum added to Core/Models
  - `IEconomicCalendarService` interface added to Core/Interfaces
  - `NewsFilterOptions` config class added to Core/Configuration (bound from `"NewsFilter"` section)
  - `ForexFactoryCalendarService` — fetches `https://www.forexfactory.com/calendar/export?format=ical`, in-memory cache with configurable refresh (default 6h), thread-safe double-checked locking; Ical.Net 5.2.1 used for iCal parsing; fail-open on fetch/parse errors
  - `NewsCalendarFilter : ISignalFilter` — extracts both currencies from instrument, checks ±window around each event, blocks with reason string like "News blackout: USD Nonfarm Payrolls in 14 min"; fail-open on all exceptions
  - `NewsFilter` appsettings section added to Worker and Api (Enabled=true, ±30 min window, High impact threshold)
  - `ForexFactoryCalendarService` registered as singleton; `NewsCalendarFilter` added as 3rd filter in CompositeSignalFilter
  - `InternalsVisibleTo(TradeFlowGuardian.Tests)` added to Infrastructure csproj for white-box parser tests
  - 11 tests added (6 NewsCalendarFilter, 5 ForexFactoryParser) — all 16 tests pass

### 2026-04-11 (session 4)
- Daily drawdown circuit breaker implemented (Phase 2)
  - `IDailyDrawdownGuard` interface added to `Core/Interfaces/IServices.cs`
  - `DailyDrawdownGuard` in `Infrastructure/Drawdown/` — Redis-backed, date-keyed (`drawdown:nav:{yyyyMMdd}`, `drawdown:breached:{yyyyMMdd}`), 48h TTL; resets automatically at UTC midnight with no scheduler
  - `DailyDrawdownFilter : ISignalFilter` — blocks Long/Short entries when breached; Close signals unaffected (handled before filters)
  - Worker wired: guard registered singleton, filter inserted second in composite (`SignalAge → DailyDrawdown → AtrSpike → News`); `SignalExecutionHandler` calls `EnsureDayOpenNavAsync` (SetNX, once/day) then `CheckAndMarkIfBreachedAsync` after each balance fetch — closes race window between filter check and balance read
  - `GET /api/status/filters` added to StatusController — returns `dailyDrawdown.{isBreached, dayOpenNav, currentBalance, drawdownPercent, maxDrawdownPercent, tradingDay}`
  - `IDailyDrawdownGuard` registered in Api DI for status endpoint
  - Breach warning logs exactly once per day via SetNX on breached key
  - All 28 tests passing; `_drawdownGuardMock` (default: not breached) wired into all handler and controller test constructors

### 2026-04-11 (session 5)
- Completed remaining StatusController endpoints and dashboard wiring
- `GET /api/status/positions` — calls new `GetAllOpenPositionsAsync` on `IOandaClient`; hits OANDA `/v3/accounts/{id}/openPositions`, returns `[{instrument, units, unrealizedPL, averagePrice}]`; `OpenPositionSummary` record added to Core/Models
- `POST /api/status/pause` — toggles global pause via `IPauseState`; body `{paused: bool}`; logs warning on every toggle
- Global pause infrastructure: `IPauseState` interface in Core; `RedisPauseState` in `Infrastructure/Pause/` — key `tradeflow:paused`, no TTL, absent=running; `GlobalPauseFilter` blocks Long/Short when set; registered singleton in both Api and Worker; inserted as second filter in composite (`SignalAge → GlobalPause → DailyDrawdown → AtrSpike → News`)
- `GET /api/status/filters` updated to include `paused` field alongside `dailyDrawdown`
- Dashboard `FilterStatus.tsx` updated — shows `Paused` and `Drawdown Limit Breached` indicators with live drawdown percentage subtitle; removed stale `atrSpike`/`newsBlocked` placeholders (those are per-signal, not persistent system state)
- `FilterStatusResponse` TypeScript type updated to match new API shape; `PauseToggle` works unchanged (reads `data.paused` from `getFilterStatus()`)
- All 28 tests passing; `_pauseStateMock` wired into `StatusControllerTests`

### Next session goals
- Phase 2 final item: PostgreSQL trade history (schema + repository)
- Schema: id, instrument, direction, entry_price, sl, tp, units, fill_price, order_id, success, error_message, executed_at (UTC)
- Use Npgsql + Dapper (no EF); repository interface in Core, implementation in Infrastructure
- Migration SQL in `docs/migrations/`; connection via `IOptions<PostgresConfig>`
- Worker writes after every order attempt; Api reads for future history endpoint
