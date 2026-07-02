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

### 2026-04-27 — Pine Script signal payload fix
- Root cause: TV alert message box used `"atr": {{close}}` — maps bar close price (~159.497)
  into the ATR field; Worker saw Price=0, Atr=159.497 and aborted on the ATR-based SL/TP
  guard (requires Price > 0 when StopLoss/TakeProfit are not pre-supplied)
- TV message box can only interpolate built-in placeholders (`{{close}}`, `{{open}}` etc.),
  not custom Pine Script variables — so ATR, SL, TP can never be sent correctly that way
- Fix: created `pine/usdjpy_emac_signal.pine` — uses `alert()` + `str.format()` to embed
  runtime values (close, atr, longSL/longTP, shortSL/shortTP) directly in the JSON body
- Payload now includes all fields the C# `TradeSignal` model expects:
  `instrument`, `direction`, `price`, `atr`, `stopLoss`, `takeProfit`, `riskPercent`, `idempotencyKey`
- Pre-calculated SL/TP path in `SignalExecutionHandler` (line 192-199) is taken — server skips
  ATR re-calculation, Price=0 guard is irrelevant; both SL and TP are authoritative from Pine
- Close signal fires before the opposing-direction entry on EMA flip bars (declaration order
  preserved by `alert.freq_once_per_bar_close`) so server clears position before new entry
- `docs/DEPLOYMENT.md` "Alert message body" section updated — instructs blank message box,
  shows example payload, explains why `alert()` must own the body
- No changes to C# execution logic, risk calculations, or field definitions

### 2026-05-06 — Staging environment setup
- Created `develop` branch from `main` as the staging deployment source
- Updated `docs/DEPLOYMENT.md`:
  - Platform diagram now shows both environments with isolated plugins
  - New "Environments & Branching" section covering:
    - `main` → production, `develop` → staging branch mapping
    - Step-by-step Railway dashboard instructions to set branch per service per environment
    - Service isolation verification steps (Redis + Postgres hostname check)
    - Per-environment env var diff table (ASPNETCORE_ENVIRONMENT, ENVIRONMENT, Webhook__Secret, connection strings)
    - Staging → production promotion workflow (PR `develop` → `main`)
    - Per-environment migration instructions via Railway CLI
  - Added `ENVIRONMENT=production/staging` plain tag var to both service env var tables
  - Added `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT`, `ENVIRONMENT` rows to complete reference table
- `railway.toml` files intentionally not changed — branch selection is a Railway dashboard setting only

### 2026-06-03 — Stage 2 dashboard + risk settings DB

**Risk settings**
- `RiskSettings` entity added to Core — `Instrument` (PK), `RiskPercent`, `IsActive`, `UpdatedAt`
- `IRiskSettingsRepository` interface added; `RiskSettingsRepository` (EF Core) + `NoOpRiskSettingsRepository` (fallback when Postgres unconfigured) added to Infrastructure
- `TradeFlowDbContext` added to `Infrastructure/Data/` — snake_case column mapping, seed data (USD_JPY / EUR_USD / GBP_USD at 1.5% active)
- EF migration `20260603000000_InitRiskSettings` + model snapshot written manually; design-time factory added for future `dotnet ef migrations add`
- `docs/migrations/003_risk_settings.sql` created — plain SQL equivalent, `IF NOT EXISTS` + `ON CONFLICT DO NOTHING` for safe re-runs
- `PostgresConnectionHelper.Normalize()` extracted to shared utility in `Infrastructure/Data/` — converts Railway's `postgresql://` URI to Npgsql key=value format; replaces duplicate private method in `TradeHistoryRepository`; used in all three `AddDbContext` / `new NpgsqlConnection` call sites
- `PositionSizer` updated — reads `RiskPercent` from DB per instrument; signal `RiskPercent > 0` override still applies; falls back to `RiskConfig.DefaultRiskPercent` when no DB row
- `SignalExecutionHandler` updated — checks `IsActive` after filters; blocks signal and increments `instrument_inactive` metric if false
- `Npgsql` upgraded 9.0.3 → 10.0.2 to satisfy `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1` transitive requirement

**API endpoints**
- `RiskController` added: `GET /api/risk`, `PATCH /api/risk/{instrument}`, `POST /api/risk/pause-all`, `POST /api/risk/resume-all` — all broadcast risk change events via SignalR
- `StatusController` extended: `GET /api/status` (combined balance + positions), `GET /api/status/trades` (90-day paired entry+close records via lateral join SQL)
- `PairedTradeRecord` model added to Core; `GetPairedTradesAsync` added to `ITradeHistoryRepository` and implemented in `TradeHistoryRepository`

**SignalR hub**
- `TradingHub` added at `/hubs/trading`
- `RedisEventSubscriberService` (hosted service) — subscribes to `tradeflow:events` Redis pub/sub channel, forwards JSON payloads to all SignalR clients
- Worker publishes `order_filled` and `position_closed` events after every OANDA call
- Worker publishes `drawdown_breached` event when daily drawdown limit is hit
- `StatusController.SetPause` broadcasts `pause_changed` directly via `IHubContext<TradingHub>`
- `RiskController` broadcasts `risk_updated` / `risk_bulk_updated` directly

**Dashboard (React)**
- `@microsoft/signalr ^8.0.7` added; `/hubs` WebSocket proxy added to `vite.config.ts`
- `App.tsx` rewritten — two-tab layout (Guard / P&L) with monospace terminal theme
- **Guard tab** — balance + drawdown banner; per-instrument cards (side badge, entry price, units, unrealised P&L, risk% stepper at 0.1% increments, active/inactive toggle); macro pause/resume button; SignalR live updates for all state changes; 30s polling fallback
- **P&L tab** — 5-week selectable SVG/CSS bar chart; per-pair breakdown (trade count, total P&L, win rate); trade list with entry→exit, duration, P&L in quote currency; 60s polling
- `useSignalR` hook — auto-reconnect with back-off (0/2/5/10/30s); silent fallback if hub unreachable
- `api/client.ts` extended — `getStatus`, `getTrades`, `getRiskSettings`, `updateRisk`, `pauseAll`, `resumeAll`
- `RISK_STEP` changed from 0.5 to 0.1 (user preference)

**Tests**
- `StatusControllerTests` updated — `Mock<IHubContext<TradingHub>>` wired in; `IHubClients` + `IClientProxy` mocks set up
- `PositionSizerTests` updated — `Mock<IRiskSettingsRepository>` (returns null) passed to `BuildSizer`
- `SignalExecutionHandlerTests` updated — `Mock<IRiskSettingsRepository>` + `Mock<ISubscriber>` added; all 4 handler constructions updated
- **35/35 tests passing**

**Migrations run on Railway**
- `001_trade_history.sql` — already present
- `002_backtest_tables.sql` — applied
- `003_risk_settings.sql` — applied; rows confirmed: EUR_USD / GBP_USD / USD_JPY at 1.5%

### Next session goals
- True realised P&L in AUD for `/trades` endpoint — OANDA transaction history API or store exit price + realised PnL on Close records
- Real-time position P&L ticking via SignalR (currently snapshot on fill only)
- GitHub Actions CI/CD pipeline
- Cloudflare DNS + SSL
- Backtest results panel in dashboard (run history, equity curve chart)

### 2026-06-11 — Account management system (no more env vars)
**Problem found:** Api and Worker each carried their own `Oanda__AccountId`/`Oanda__ApiKey`/`Oanda__Environment` env vars (Railway + docker-compose). They had drifted — Api pointed at the live account while the Worker executed on fxpractice. Nothing in code kept them in sync.

**Fix — shared encrypted account registry:**
- `oanda_accounts` table (migration `004_oanda_accounts.sql`) — label, account_id, environment, encrypted API key; partial unique index enforces exactly one active account; both services read the same row
- API keys encrypted at rest via ASP.NET Data Protection (`Microsoft.AspNetCore.DataProtection.StackExchangeRedis` 10.0.9) — keys persisted to Redis under `tradeflow:dataprotection-keys`, app name `TradeFlowGuardian`, so Api-written ciphertext is Worker-readable. Zero OANDA env vars remain
- `IOandaAccountStore` (EF CRUD, `OandaAccountRepository`) + `IActiveAccountProvider` (`ActiveAccountProvider` singleton: 30s cache, Redis pub/sub invalidation on `tradeflow:account-changed`, fallback to legacy `Oanda` config section when registry empty)
- `OandaClient` rewired — resolves the active account per request (URL + bearer per call); account switch takes effect without restart in both services
- `AccountsController` — GET/POST `/api/accounts`, PUT `/{id}/activate`, DELETE, GET `/accounts/active`; secured via `X-Admin-Secret` header (= webhook secret); activating fxtrade requires `confirmLive=true`; API keys are write-only; publishes `account_changed` to `tradeflow:events` (SignalR) + invalidation channel
- `AccountSeedService` (Api) — one-time idempotent seed from the `Oanda` config section if the table is empty
- Dashboard "Acct" tab — active-account banner (LIVE/DEMO badge), admin-secret unlock, account list with activate/delete (typed "LIVE" confirmation for fxtrade), add-account form; SignalR `account_changed` refresh
- docker-compose: `OANDA_API_KEY`/`OANDA_ACCOUNT_ID` env vars removed from api + worker
- Bumped vulnerable `Microsoft.AspNetCore.DataProtection` transitive chain → `Microsoft.Extensions.*` 10.0.9 across Infrastructure/Backtesting/Tests/Worker (NU1903/NU1904 cleared)

**Tests** — `OandaAccountRepositoryTests` (SQLite in-memory, encryption round-trip, single-active invariant), `ActiveAccountProviderTests` (registry/fallback/cache), `AccountsControllerTests` (auth, confirmLive gate). **55/55 passing**

**Deploy steps (manual):**
1. Run `docs/migrations/004_oanda_accounts.sql` against Railway Postgres
2. Deploy Api + Worker — Api seeds the registry from the existing env vars on first boot
3. Verify GET `/api/accounts` shows the seeded account, register the correct demo/live accounts via the dashboard Acct tab
4. Delete `Oanda__*` env vars from BOTH Railway services

### Next session goals
- Delete Railway `Oanda__*` env vars once the registry is confirmed seeded
- True realised P&L in AUD for `/trades` endpoint
- GitHub Actions CI/CD pipeline

### 2026-06-12 (session 2)
- **Grafana Cloud centralized logging** (`feature/grafana-cloud-logs`)
  - `OtlpLoggingExtensions.AddOtlpExportIfConfigured()` (Infrastructure/Logging) — OpenTelemetry OTLP log exporter alongside the existing console providers; activates only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, so dev/tests/CI are untouched
  - Wired into Api (`tradeflow-api`) and Worker (`tradeflow-worker`); `deployment_environment` label from `RAILWAY_ENVIRONMENT_NAME`
  - Package: OpenTelemetry.Exporter.OpenTelemetryProtocol 1.16.0 (net10 compatible)
  - Verified end-to-end against a local OTLP listener: batched protobuf POSTs to /v1/logs with auth header, console JSON unaffected; 55/55 tests pass
  - `docs/LOGGING.md` — Grafana Cloud token setup, the 3 Railway env vars, LogQL examples

### Next session goals
- Set OTEL_* env vars on Api + Worker in Railway (staging first) and confirm logs in Grafana Drilldown
- Adopt the migration runner (PR #21): baseline staging + prod, set pre-deploy command
### 2026-06-12
- **SQL migration runner + pre-deploy entry points** (`feature/migration-runner`) — replaces manual paste-into-Railway-console migrations
  - `SqlMigrationRunner` (Infrastructure/Data) — hand-rolled on Npgsql/Dapper (~200 lines, fits the no-EF-migrations philosophy; DbUp rejected to avoid a new dependency)
  - `docs/migrations/*.sql` embedded into the Api assembly (linked, single source of truth stays in docs/)
  - `schema_versions` table (version, name, applied_at, checksum); SHA-256 checksum verified on every run — editing an applied migration hard-fails the deploy
  - `pg_advisory_lock` for the whole run — concurrent instances can't double-apply (integration-tested with a pg_sleep race)
  - Each migration in its own transaction; first failure stops the run with exit 1 so Railway aborts the deploy
  - `--migrate-only` and `--migrate-baseline N` exit without starting Kestrel/hosted services; normal startup never migrates (logs a warning if pending detected)
  - Tests: 15 new (unit + integration gated behind `TFG_TEST_POSTGRES`, scratch DB per test) — 70/70 passing
  - Smoke-tested CLI end-to-end against docker-compose Postgres: fresh apply (001–004), no-op rerun, baseline, baseline-refusal, exit codes
  - `docs/MIGRATIONS.md` — how to add a migration, immutability rule, staging/prod adoption plan

### Next session goals
- Adopt the runner: baseline staging + prod (`--migrate-baseline 4`), set Railway pre-deploy command
- Delete Railway `Oanda__*` env vars once the registry is confirmed seeded
- True realised P&L in AUD for `/trades` endpoint
- GitHub Actions CI/CD pipeline

### 2026-06-12 (session 2)
- **Broker abstraction seam** (`feature/broker-abstraction`) — OANDA is now one adapter behind a port; pure refactor, zero behavior change
  - `IBrokerClient` port in `Core/Brokers/` (+ `BrokerDescriptor`, `BrokerTransaction`); `IOandaClient` deleted — signatures unchanged, all crossing types were already Core-owned
  - `OandaClient` → `Infrastructure/Brokers/Oanda/OandaBrokerClient` (git mv, method bodies untouched); all v20 quirks (FOK, 5dp/3dp formatting, close ALL/NONE) stay inside the adapter
  - Hardcoded 30:1 leverage moved from PositionSizer const into `OandaBrokerClient.Descriptor` ("oanda", 30m — API's reported 100:1 still ignored); PositionSizer moved to `Infrastructure/Sizing/`
  - `GetTransactionsAsync` on the port for upcoming realised-P&L work; adapter throws NotImplementedException for now
  - Migration `005_broker_column.sql` — additive `broker` discriminator on oanda_accounts (default 'oanda'), not yet read by code
  - 6 new `OandaBrokerClientMappingTests` pin the exact outgoing OANDA wire requests (capturing HttpMessageHandler) — 67/67 passing
  - `docs/BROKER_ABSTRACTION.md` — port surface, canonical EUR_USD instrument format, new-adapter checklist, deferred follow-ups

### 2026-07-02
- **Margin cap made configurable + raised** — diagnosed why live USD_JPY trades risked ~$600 instead of the configured 2.5% (~$2,600): the hardcoded 28% margin-utilisation cap in `PositionSizer` binds on tight-stop JPY signals (risk formula wants ~2.5–3.3M units; cap allowed ~617k), so the risk % was never reached
  - `RiskConfig.MarginUtilisationLimit` added (default `0.40`); `PositionSizer` reads it instead of the `const 0.28m` — at current prices USD_JPY caps at ~880k units (~0.86% effective risk) instead of ~617k (~0.6%)
  - Warning now logged whenever the cap (or `MaxPositionUnits`) overrides the risk-based size, including the effective risk % actually taken
  - `Risk:MarginUtilisationLimit: 0.40` added to Api + Worker appsettings; documented in CONFIGURATION.md and DEPLOYMENT.md (tunable via `Risk__MarginUtilisationLimit` env var, no redeploy of code needed)
  - Tests: `BuildSizer` takes explicit `marginUtilisationLimit` (0.28 default preserves existing expectations); new test pins that the config value scales the cap — 68/68 passing
  - Note: next ceiling is `MaxPositionUnits` (1,000,000) — reaching the earlier-discussed 1.5M units would need that raised too, plus margin limit ~0.68

### 2026-07-02 (session 2)
- **Backtest engine margin/currency honesty** — first step toward dropping Pine and backtesting apples-to-apples against the live system
  - `PositionSizeCalculator` added to Core (`Core/Sizing/`) — the single pure sizing formula (risk units + margin cap + max-units cap, reports which cap bound and effective risk %); live `PositionSizer` now delegates to it, behavior unchanged (existing PositionSizerTests pass untouched = parity proof)
  - `BacktestEngine` sizing rewritten: was `units = riskAmount / stopDistance` with no quote-currency conversion (units ~112× too small on JPY pairs, margin unmodellable) — now uses the shared calculator with `Leverage` (30), `MarginUtilisationLimit` (0.40), `MaxPositionUnits` (1M), `QuoteToAccountRate` (static per-quote-currency fallbacks matching PositionSizer's) on `BacktestRequest`; warns when a cap binds, same as live
  - `BacktestTrade` P&L now converts quote → account currency (`QuoteToAccountRate` on the trade); `RMultiple`, `PnLPercent` consistent; spread cost fixed (was subtracting a raw pip count from currency P&L), commission now on real unit counts
  - `BacktestApiRequest` exposes the four new knobs on POST /api/backtest/run
  - Tests: `PositionSizeCalculatorTests` (7) pin the shared formula incl. the 55,059/78,662 margin-cap traces; `BacktestEngineSizingTests` (2) run the engine end-to-end with a stub strategy — tight-stop JPY trade margin-caps at 78,400 units with exact AUD P&L, wide-stop trade gets full 2.5% risk — 78/78 passing
  - **Known limitation**: quote→account rate is constant per run (no historical AUD cross rates); fine for margin feasibility, revisit for multi-year precision
  - **Next milestone**: port the Pine TFG strategy (SMA 9/25 cross, EMA 179 trend, RSI 18, ATR 13 SL/TP) into StrategyFactory as a preset so the SL-multiplier sweep can run against the real strategy — current presets are EMAC-only and don't emit ATR-based SL/TP

### 2026-07-02 (session 3)
- **Pine TFG v5 strategy ported to C#** (`tfg_usdjpy_v5` preset) — the "drop Pine" milestone: signals are now generatable server-side and the SL sweep can run against the real strategy
  - `TfgV5Signal` (Strategies/Signals/Tfg/) — 1:1 port of `TFG_USDJPY_live.pine` entry logic: SMA 9/25 cross + EMA 179 trend + RSI 18 (Wilder) + ATR 13 rising + EMA dist 5–69 pips; SL/TP = close ∓ slMult/tpMult × ATR (parameterised, defaults 2.6/5.3); indicators computed over bounded trailing windows (tail weight < 0.1%) so per-bar re-evaluation stays O(window) not O(N²)
  - Session gate (00–09 + 11–12 UTC) as `OrFilter(TimeFilter, TimeFilter)` in the preset's `FilteredSignalRule`, evaluated on the engine-supplied bar timestamp — visible in pipeline traces
  - `StrategyFactory.Create` gains optional `slMultiplier`/`tpMultiplier`; `BacktestApiRequest` exposes them (`SlMultiplier`/`TpMultiplier`) → sweeps via POST /api/backtest/run
  - `scripts/sweep-usdjpy-sl.sh` — sweeps SL 2.6/4/6/8× ATR (TP at Pine's 2.04 ratio) at 2.5% risk under the honest margin model, prints comparison table; needs Api + cached M15 candles
  - Tests: 6 new `TfgV5SignalTests` — dip-pop series fires Long with every Pine gate verified via diagnostics, SL/TP exactness, flat-market never fires, session in/out via preset, multiplier pass-through — 84/84 passing
  - **Behavior note (intentional)**: like the live Worker (no-pyramid rule), the engine ignores opposite signals while a position is open — exits are SL/TP only. TV's strategy tester *reverses* on opposite entries, so the original Pine backtest differs here; ours matches live
