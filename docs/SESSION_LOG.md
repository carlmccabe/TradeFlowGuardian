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

### 2026-04-11 (session 4) — Railway deployment
- Deployed `TradeFlowGuardian.Api` to Railway
  - `railway.toml` created at repo root — specifies `dockerfilePath = "TradeFlowGuardian.Api/Dockerfile"` and `buildContext = "."` (required because Dockerfile copies sibling projects)
  - Dockerfile PORT handling fixed — replaced hardcoded `ENV ASPNETCORE_URLS=http://+:8080` with `CMD ["sh", "-c", "exec dotnet ... --urls http://+:${PORT:-8080}"]` so Railway's injected `PORT` is honoured; `exec` keeps dotnet as PID 1
  - Health check path `/`, restart policy ON_FAILURE
- Pre-deploy fixes
  - `PriceController` — added missing `[ApiController]` and `[Route("api/[controller]")]`; route was resolving to `/price/{instrument}` instead of `/api/price/{instrument}`
  - `Program.cs` — added TODO comment on `/metrics` endpoint re: private network restriction
  - `TECH_DEBT.md` — added open item for direct `IConfiguration` read of `Redis:ConnectionString` in `Program.cs` (should use `IOptions<RedisConfig>`)
- Deployed `TradeFlowGuardian.Worker` to Railway
  - `TradeFlowGuardian.Worker/railway.toml` created — Root Directory set to `TradeFlowGuardian.Worker/`, `buildContext = ".."` to reach repo root
  - `KestrelMetricServer` port made dynamic — reads `PORT` env var (Railway-injected) with fallback to 9091 for local docker-compose; without this Railway health-checks the injected PORT, finds nothing listening, and restart-loops
  - Health check path `/metrics`, restart policy ON_FAILURE
- Diagnosed `GET /api/status/balance` returning `balanceAud: 0` — root cause was mismatched OANDA account/API key; resolved by user. Documented the silent-failure pattern in `GetAccountBalanceAsync` (catches all exceptions, returns 0 — actual error only visible in Railway logs)
- Improved logging for Railway
    - Both `Program.cs`: `ClearProviders()` + conditional format — Development keeps coloured multi-line console; Production uses `SingleLine = true` so each entry is one line in Railway's log stream
    - Both `Program.cs`: startup config banner logs OANDA env/URL, Redis host (credentials stripped), stream, consumer (Worker), active filters — confirms loaded config at a glance on deploy
    - Both `appsettings.json`: `StackExchange` and `System.Net.Http.HttpClient` set to `Warning`; `Microsoft.Hosting.Lifetime` kept at `Information`
- Graceful shutdown hardening
    - Both `Program.cs`: `HostOptions.ShutdownTimeout = 30 s` — matches Railway's SIGTERM-to-SIGKILL window (up from .NET default of 5 s)
    - `SignalExecutionHandler`: `PlaceMarketOrderAsync`, `ClosePositionAsync`, and post-fill `positionCache.SetAsync`/`ClearAsync` all use `CancellationToken.None` — market operations must not be aborted mid-flight; OCE in `HandleAsync` now only fires during pre-order checks and log says so explicitly
    - `ExecutionWorker`: `StopAsync` override logs "shutdown requested" / "stopped cleanly" around `base.StopAsync`; idle `DequeueAsync` OCE caught explicitly and logged as "idle at shutdown"

### 2026-04-11 (session 5)
- Daily drawdown circuit breaker implemented (Phase 2)
  - `IDailyDrawdownGuard` interface added to `Core/Interfaces/IServices.cs`
  - `DailyDrawdownGuard` in `Infrastructure/Drawdown/` — Redis-backed, date-keyed (`drawdown:nav:{yyyyMMdd}`, `drawdown:breached:{yyyyMMdd}`), 48h TTL; resets automatically at UTC midnight with no scheduler
  - `DailyDrawdownFilter : ISignalFilter` — blocks Long/Short entries when breached; Close signals unaffected (handled before filters)
  - Worker wired: guard registered singleton, filter inserted second in composite (`SignalAge → DailyDrawdown → AtrSpike → News`); `SignalExecutionHandler` calls `EnsureDayOpenNavAsync` (SetNX, once/day) then `CheckAndMarkIfBreachedAsync` after each balance fetch — closes race window between filter check and balance read
  - `GET /api/status/filters` added to StatusController — returns `dailyDrawdown.{isBreached, dayOpenNav, currentBalance, drawdownPercent, maxDrawdownPercent, tradingDay}`
  - `IDailyDrawdownGuard` registered in Api DI for status endpoint
  - Breach warning logs exactly once per day via SetNX on breached key
  - All 28 tests passing; `_drawdownGuardMock` (default: not breached) wired into all handler and controller test constructors

### 2026-04-11 (session 6)
- Completed remaining StatusController endpoints and dashboard wiring
- `GET /api/status/positions` — calls new `GetAllOpenPositionsAsync` on `IOandaClient`; hits OANDA `/v3/accounts/{id}/openPositions`, returns `[{instrument, units, unrealizedPL, averagePrice}]`; `OpenPositionSummary` record added to Core/Models
- `POST /api/status/pause` — toggles global pause via `IPauseState`; body `{paused: bool}`; logs warning on every toggle
- Global pause infrastructure: `IPauseState` interface in Core; `RedisPauseState` in `Infrastructure/Pause/` — key `tradeflow:paused`, no TTL, absent=running; `GlobalPauseFilter` blocks Long/Short when set; registered singleton in both Api and Worker; inserted as second filter in composite (`SignalAge → GlobalPause → DailyDrawdown → AtrSpike → News`)
- `GET /api/status/filters` updated to include `paused` field alongside `dailyDrawdown`
- Dashboard `FilterStatus.tsx` updated — shows `Paused` and `Drawdown Limit Breached` indicators with live drawdown percentage subtitle; removed stale `atrSpike`/`newsBlocked` placeholders (those are per-signal, not persistent system state)
- `FilterStatusResponse` TypeScript type updated to match new API shape; `PauseToggle` works unchanged (reads `data.paused` from `getFilterStatus()`)
- All 28 tests passing; `_pauseStateMock` wired into `StatusControllerTests`

### 2026-04-11 (session 7)
- PostgreSQL trade history implemented — Phase 2 complete
  - `PostgresConfig` added to `Core/Configuration/AppConfig.cs` (bound from `"Postgres"` section)
  - `TradeHistoryRecord` record added to `Core/Models/` — all schema fields as nullable where appropriate
  - `ITradeHistoryRepository` interface added to `Core/Interfaces/IServices.cs`
  - `TradeHistoryRepository` in `Infrastructure/History/` — Npgsql + Dapper, new connection per call; log-and-swallow on failure (DB outage must not abort trade workflow)
  - `Npgsql 9.0.3` and `Dapper 2.1.35` added to `Infrastructure.csproj`
  - `docs/migrations/001_trade_history.sql` created — `trade_history` table with `BIGSERIAL` PK, `TIMESTAMPTZ` executed_at, two indexes (instrument, executed_at DESC)
  - `Postgres:ConnectionString` section added to both `appsettings.json` files
  - `ITradeHistoryRepository` registered as scoped in both Worker and Api `Program.cs`
  - `SignalExecutionHandler` updated — `InsertAsync` called with `CancellationToken.None` after every `PlaceMarketOrderAsync` (success or failure) and `ClosePositionAsync`; entry_price/sl/tp/units populated from computed values; close records use 0/null for N/A fields
  - Pre-existing `Worker.cs` missing `using TradeFlowGuardian.Core.Models` fixed
  - `_tradeHistoryMock` wired into all 4 `SignalExecutionHandlerTests` constructors
  - All 28 tests passing

### 2026-04-12
- Switched webhook auth from HMAC-SHA256 header to `?secret=` query parameter
  - TradingView webhooks cannot send custom headers, so X-Signature was unreachable
  - `HmacValidationMiddleware` rewritten: reads `context.Request.Query["secret"]`, compares to `WebhookConfig.Secret` with `CryptographicOperations.FixedTimeEquals` (constant-time, prevents timing attacks)
  - `ValidateSignature` method and `SignatureHeader` const removed; body buffering retained for downstream controllers
  - `WebhookConfig.Secret` property unchanged — same env var binding path
  - TradingView alert URL format: `https://<host>/api/signal?secret=YOUR_SECRET`
  - HTTPS ensures secret is not transmitted in plaintext

### 2026-04-12 (session 2) — Backtest engine integration
- Integrated `backtest-engine-extract/` into the solution as three new projects:
  - `TradeFlowGuardian.Domain` — `Candle`, `IStrategy`, `Decision`, `TradeAction`, pipeline interfaces
  - `TradeFlowGuardian.Strategies` — `EmaIndicator`, `EmaCrossoverSignal`, `PipelineBuilder`, `FilteredSignalRule`, composable filters
  - `TradeFlowGuardian.Backtesting` — `BacktestEngine`, `BacktestDataContext` (EF Core), `OandaHistoricalProvider`
- Namespaces renamed from `ForexApp.*` → `TradeFlowGuardian.*` (user-completed before session)
- Fixed missing NuGet packages: `RestSharp 112.1.0` and `Newtonsoft.Json 13.0.3` added to `Infrastructure.csproj` (backtest OANDA HTTP client files were copied in without their packages — blocked the build with 37 errors)
- Migrated `BacktestDataContext` from SQL Server to PostgreSQL:
  - Replaced `Microsoft.EntityFrameworkCore.SqlServer` → `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0`
  - Removed `.IsClustered()` and `.IncludeProperties()` from `BacktestDataContext.OnModelCreating` (SQL Server-only EF fluent API)
- `PipelineStrategy` adapter added — wraps `IPipeline` to satisfy `IStrategy` contract; maps `Core.TradeAction` (EnterLong/Short/ExitPosition) → `Domain.TradeAction` (Buy/Sell/Exit)
- `StrategyFactory` added — named preset resolution: `emac_10_30`, `emac_9_21`, `emac_12_26`, `emac_custom`
- `BacktestServicesExtensions.AddBacktestServices()` — single extension method wires all DI: `OandaOptions`, `RestClient`, `OandaHttpClient`, `IOandaApiService`, `IHistoricalDataProvider`, `IBacktestEngine`, `BacktestDataContext`
- `BacktestController` added to API — `POST /api/backtest/run`, `GET /api/backtest/runs`, `GET /api/backtest/runs/{id}`, `GET /api/backtest/strategies`
- `docs/migrations/002_backtest_tables.sql` created — `HistoricalCandles`, `BacktestRuns`, `BacktestTrades`, `BacktestEquityCurve`
- `docs/BACKTEST.md` created — API usage, strategy presets, migration steps, cURL examples
- Build clean: 0 errors, 2 pre-existing warnings
- **Pending:** run `002_backtest_tables.sql` against PostgreSQL before first backtest call

### Next session goals
- Run `docs/migrations/001_trade_history.sql` and `002_backtest_tables.sql` against Railway Postgres
- Set `Postgres:ConnectionString` in Railway env vars for both Api and Worker
- Add more strategy presets to `StrategyFactory` (ADX-filtered EMAC, RSI mean-reversion)
- Phase 3 dashboard: backtest results panel (run history, equity curve chart)
- Phase 3 dashboard: P&L chart (daily/weekly) using trade_history table
- Phase 3 dashboard: SignalR hub for real-time P&L push
- Phase 4: GitHub Actions CI/CD
