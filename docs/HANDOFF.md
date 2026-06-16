# TradeFlow Guardian — Maintainer Handoff Report

*Last updated: 2026-06-12. Written for an engineer taking over maintenance with no prior context.*

> ⚠️ **This system trades real money.** The production Worker executes market orders on a
> live OANDA `fxtrade` account. Treat every production change accordingly: staging first,
> verify, then promote. When in doubt, use the kill switch (below) before debugging.

---

## 1. What this is

An API-native forex trade execution engine that replaced a legacy MT4 Expert Advisor + VPS
setup. TradingView strategy alerts POST a webhook → the Api validates and queues the signal
→ the Worker applies risk filters and executes via the OANDA v20 REST API → every order
attempt is written to PostgreSQL.

```
TradingView Alert (webhook POST, ?secret= query param)
        ↓
TradeFlowGuardian.Api          ASP.NET Core 10 — validates, queues, dashboard/status API
        ↓ (Redis Stream: tradeflow:signals, consumer group: workers)
TradeFlowGuardian.Worker       .NET Worker Service — filters, sizes, executes
        ↓                                    ↓
OANDA v20 REST API             PostgreSQL (trade_history, risk_settings, oanda_accounts)
```

A React PWA dashboard (`TradeFlowGuardian.Dashboard/`) shows balance, positions, filter
status, and provides per-instrument kill switches and a global pause toggle.

**Start here:** [CLAUDE.md](../CLAUDE.md) is the canonical conventions/architecture
document and is kept current. This handoff covers what it doesn't: operations, deployed
infrastructure, and tribal knowledge.

## 2. Repository layout

| Project | Role |
|---|---|
| `TradeFlowGuardian.Core` | Models, interfaces, config records — zero dependencies |
| `TradeFlowGuardian.Infrastructure` | OandaClient, filters, Redis queue/cache, repositories, migration runner, OTLP logging |
| `TradeFlowGuardian.Api` | Webhook receiver, status/kill-switch/accounts endpoints, SignalR hub, migration CLI |
| `TradeFlowGuardian.Worker` | Execution loop (`ExecutionWorker` → `SignalExecutionHandler`) |
| `TradeFlowGuardian.Dashboard` | Vite + React 18 + TS + Tailwind v4 PWA |
| `TradeFlowGuardian.Backtesting` / `Strategies` / `Domain` | Backtest engine and strategy signals (used by Api backtest endpoints) |
| `TradeFlowGuardian.Tests` | xunit + Moq; Postgres integration tests gated behind `TFG_TEST_POSTGRES` env var |
| `pine/` | TradingView Pine Script source (the signal-generating strategy) |
| `docs/` | All operational docs (this file, MIGRATIONS, LOGGING, SECRETS, DEPLOYMENT, CONFIGURATION, BACKTEST, TECH_DEBT, SESSION_LOG) |
| `monitoring/` | Local-only Grafana/Loki/Prometheus stack for docker-compose — **not deployed anywhere** |

Key non-negotiable conventions (full list in CLAUDE.md): no pyramiding (one position per
instrument), idempotency keys on every signal, all OANDA calls through `IOandaClient`,
config via `IOptions<T>`, OANDA credentials only via `IActiveAccountProvider`, migrations
are immutable plain SQL.

## 3. Deployed infrastructure (Railway)

Project **`skillful-balance`** (id `79bf3133-593f-4e67-a0c7-d49fec72afae`), two
environments. **Service names do not all match repo names:**

| Railway service | What it actually is | Staging domain | Production domain |
|---|---|---|---|
| `TradeFlowGuardian` | **Api** | tradeflowguardian-staging.up.railway.app | tradeflowguardian-production.up.railway.app* |
| `incredible-victory` | **Worker** | (no public domain needed) | — |
| `TradeFlowGuardian-React` | Dashboard | tradeflowguardian-react-staging.up.railway.app | tradeflowguardian-react-production.up.railway.app |
| `Postgres` | postgres-ssl:18 template | — | — |
| `Redis` | redis:8.2.1, **shared by both environments' services** | — | — |

\* confirm exact prod Api domain in Railway; the dashboard CORS config is the source of truth.

### Branch → environment mapping (the deploy pipeline)

| Git branch | Deploys to | Gate |
|---|---|---|
| `main` | **staging** (all 3 app services) | GitHub Actions CI must pass (Railway "Wait for CI" / `checkSuites: true`) |
| `production` | **production** | same CI gate |

- **Deploy to staging** = merge/push to `main`. **Promote to prod** = `git push origin main:production` (fast-forward).
- The Api service in **both** environments has a Railway **pre-deploy command**:
  `dotnet TradeFlowGuardian.Api.dll --migrate-only` — applies pending SQL migrations before
  the new version starts; non-zero exit aborts the deploy (old version keeps serving).
- Build config: [railway.toml](../railway.toml) (Dockerfile at `TradeFlowGuardian.Api/Dockerfile`,
  build context = repo root because Dockerfiles COPY sibling projects).
- The GitHub branches `staging` and `develop` are **orphaned** — nothing watches them.
  Delete them to avoid confusion.

### Configuration / environment variables

- **OANDA credentials are NOT in env vars.** They live in the `oanda_accounts` Postgres
  table (API key encrypted with ASP.NET Data Protection; keys persisted in Redis under
  `tradeflow:dataprotection-keys`). Exactly one account is active at a time, shared by Api
  and Worker via `IActiveAccountProvider` (30s cache + Redis pub/sub invalidation on
  `tradeflow:account-changed`). Manage via the dashboard "Acct" tab or
  `/api/accounts` (header `X-Admin-Secret` = webhook secret). Legacy `OANDA__*` env vars
  still exist on some services as seed/fallback only — **safe to delete once you confirm
  the registry rows exist**; the production account is the live one, labeled "Live".
- `Postgres__ConnectionString` on Api and Worker in both envs is the Railway reference
  `${{Postgres.DATABASE_URL}}`. **Do not** replace with literals or shared-variable
  indirection (see Gotchas §8).
- `Dashboard__Origin` on the Api = comma-separated allowed CORS origins (staging and prod
  each list their own dashboard URL + `http://localhost:5173`).
- `Webhook__Secret` / `HMAC__Secret` — webhook auth secret, also doubles as the admin
  secret for `/api/accounts`.
- `OTEL_EXPORTER_OTLP_*` — Grafana Cloud logging (see §5). Defined as environment-level
  **shared variables** (literal values) and referenced per service.
- Local dev secrets: macOS Keychain pattern per [SECRETS.md](./SECRETS.md); never commit
  secrets; production secrets policy per CLAUDE.md.

## 4. Database & migrations

- Schema lives in [`docs/migrations/`](./migrations/) as numbered, **immutable** SQL files
  (currently 001–004: trade_history, backtest tables, risk_settings, oanda_accounts).
- They're embedded into the Api assembly and applied by `SqlMigrationRunner`
  ([Infrastructure/Data](../TradeFlowGuardian.Infrastructure/Data/SqlMigrationRunner.cs)):
  advisory-locked, one transaction per migration, tracked in `schema_versions` with SHA-256
  checksums. Editing an applied file **fails the deploy by design** — add a new number instead.
- Full workflow + adoption history in [MIGRATIONS.md](./MIGRATIONS.md). Both databases are
  adopted: staging was created fresh by the runner; production was baselined
  (`--migrate-baseline 4`) on 2026-06-12.
- **To ship migration 005:** add `docs/migrations/005_name.sql`, merge to `main`, verify the
  staging pre-deploy log shows it applied, then promote.
- Known cruft in the **production** DB: stray tables `test` and `trad_history` (typo) from
  old manual console sessions — unused, safe to drop after double-checking they're empty.

## 5. Logging & observability

Two parallel sinks (see [LOGGING.md](./LOGGING.md)):

1. **JSON console** → Railway's per-service log viewer (always on in non-Development).
2. **OpenTelemetry OTLP → Grafana Cloud Loki** — active when `OTEL_EXPORTER_OTLP_ENDPOINT`
   is set (it is, in both environments). Batched, fire-and-forget; a Grafana outage can
   never block trading.

- Grafana stack: **https://dashingcantaloupe1611.grafana.net** (Grafana Cloud free tier,
  zone `prod-au-southeast-1`; Loki tenant `1645562` at `logs-prod-026.grafana.net`).
- Query in Drilldown → Logs, or Explore with the `grafanacloud-...-logs` data source:
  `{service_name=~"tradeflow.*"}` — services are `tradeflow-api` and `tradeflow-worker`,
  with a `deployment_environment` label (`staging`/`production`) from `RAILWAY_ENVIRONMENT_NAME`.
- The OTLP token (in the shared `OTEL_EXPORTER_OTLP_HEADERS` var) is **write-only** — you
  can't query Loki with it; use the Grafana UI.
- The Api also exposes Prometheus metrics at `/metrics` (prometheus-net). **Nothing scrapes
  it** and it is publicly reachable, unauthenticated (low sensitivity; there's a TODO in
  Program.cs to restrict it).
- The `monitoring/` folder + `docker compose --profile monitoring` is a fully local
  observability stack, unrelated to the cloud setup.

## 6. The trading path (what matters when something misfires)

1. TradingView fires an alert → `POST /api/signal?secret=...` (JSON; TradingView sends
   `Content-Type: text/plain`, handled by a custom input formatter).
2. Api validates the secret (middleware; POST `/api/signal` only — **GET endpoints are
   unauthenticated by design**), logs the raw body, enqueues to Redis Stream.
3. Worker reads via `XREADGROUP` (group `workers`), opens a DI scope per signal, runs the
   filter chain **in order**: SignalAge → GlobalPause → DailyDrawdown → AtrSpike → NewsCalendar.
4. `SignalExecutionHandler` enforces idempotency and no-pyramiding, sizes via
   `PositionSizer` (live FX rates from OANDA `/pricing`, conservative hardcoded fallback),
   places the market order with SL/TP, `XACK`s.
5. `TradeHistoryRepository.InsertAsync` records the attempt with `CancellationToken.None`,
   **log-and-swallow** — a DB outage must never mask a fill. Corollary: if the DB is
   misconfigured, trades still execute but history quietly stops being written; check logs
   for swallowed errors if `trade_history` looks sparse.

Risk parameters per instrument live in the `risk_settings` table (managed via dashboard /
API, seeded by migration 003). Current pairs: USD_JPY, EUR_USD, GBP_USD.

## 7. Operational runbooks

**Emergency stop (do this first, debug later)**
- Global pause: dashboard header toggle, or `POST /api/status/pause`.
- Close a position now: dashboard kill switch, or `POST /api/status/close/{instrument}`.
- Nuclear: in Railway, remove the Worker service's deployment (Api can keep queueing;
  signals wait in the Redis stream).

**Deploy**
- Staging: merge PR to `main`; CI runs; Railway deploys all 3 services; pre-deploy migrates.
- Production: `git push origin main:production` after verifying staging.
- Watch: Railway dashboard, or `railway logs --service <name> --environment <env>`.

**Verify a deploy**
- Api startup banner logs OANDA env, Redis, CORS, filters. Pre-deploy logs show either
  "No pending migrations" or each applied migration.
- Worker banner logs the active account: confirm `OANDA=fxtrade | Account=Live` in prod.

**Switch OANDA account** — dashboard Acct tab (live accounts require an explicit
confirmation), or `POST /api/accounts/{id}/activate` with `X-Admin-Secret`.

**Test a signal end-to-end (staging)** — `scripts/test-signal.sh` posts a sample payload;
watch the Worker logs / Grafana for the idempotency key.

**Railway API access (when the dashboard is too slow)** — the CLI is authenticated
(`railway whoami`). For config inspection/mutation the GraphQL API at
`backboard.railway.com/graphql/v2` works with the `accessToken` from
`~/.railway/config.json`. Note the CLI **rotates that token on every CLI invocation** —
re-read the file after any `railway` command.

## 8. Gotchas & tribal knowledge (hard-won, please read)

1. **Railway shared variables cannot resolve service references.** `${{Postgres.DATABASE_URL}}`
   or bare `${{PGHOST}}` inside a *shared* variable render as **empty strings** — silently.
   This took prod down on 2026-06-12 (the pre-deploy migration gate caught the Api; the
   Worker ran ~100 min with a dead DB config). Service-to-service references belong in
   **service-scoped** variables only. A leftover broken shared `Postgres__ConnectionString`
   may still exist in prod — delete it if it's still there.
2. **The Worker has no pre-deploy gate.** Only the Api validates the DB before starting.
   A bad DB config on the Worker = running service, swallowed trade-history writes, failing
   registry reads. After any DB-related var change, check Worker logs explicitly.
3. **`Cannot load library libgssapi_krb5.so.2`** appears at every container start — the
   runtime image lacks Kerberos, Npgsql probes for it. Harmless noise; ignore (or add
   `No Kerberos` to the connection string / install the lib if it offends).
4. **Webhook auth is a query param** (`?secret=`), not a header — TradingView can't send
   custom headers. The secret therefore appears in URLs/logs; rotate it if leaked.
5. **GET status endpoints are unauthenticated** by design (dashboard polls them). Don't put
   anything sensitive in them.
6. **Redis is per-environment** (one Railway Redis *service*, separate instance + volume in
   each environment; `redis.railway.internal` resolves to the local environment's instance).
   Within an environment, keys are not namespaced — the signal stream, position cache,
   pause flag, and Data Protection keys all share one Redis. Flushing it logs out the
   account registry's encryption keys; don't flush casually.
7. **Migration files are immutable post-merge** — the runner checksum-fails the deploy on
   any edit. This is a feature. New number, always.
8. **TradingView payload `direction`** is `"Long" | "Short" | "Close"` (enum-converted);
   `riskPercent: 0` means "use the per-instrument default from risk_settings".
9. **Account registry caching** — account switches propagate within ~30s via Redis pub/sub;
   don't panic if a switch isn't instant.
10. **Don't run `dotnet ef` anything.** EF Core is used as an ORM only; schema is owned by
    the SQL migration runner.

## 9. External accounts & access you need

| System | What | Notes |
|---|---|---|
| GitHub | `carlmccabe/TradeFlowGuardian` | CI via Actions (`.github/workflows/ci.yml`); branch protection not documented — verify |
| Railway | project `skillful-balance` | Deploys, env vars, Postgres/Redis, pre-deploy commands |
| Grafana Cloud | stack `dashingcantaloupe1611` | Centralized logs (free tier); OTLP write token in Railway shared vars |
| OANDA | fxpractice (staging) + fxtrade **live** (production) | API keys stored encrypted in the `oanda_accounts` table |
| TradingView | Pine strategy + alert webhooks | Alerts point at the prod Api `/api/signal?secret=...` |

## 10. Current state & open work (as of 2026-06-12)

**Recently landed:** SQL migration runner + pre-deploy adoption in both envs (PR #21);
Grafana Cloud OTLP logging live in both envs (PR #22); OANDA account registry (PR #20);
staging branch mapping fixed (staging now deploys from `main`); staging DB created and
CORS fixed; prod shared-variable incident resolved.

**Open / next up:**
- Phase 3 dashboard polish: P&L chart, SignalR real-time push (poll-based today).
- Cloudflare DNS + SSL (Phase 4) — currently on `*.up.railway.app` domains.
- Cleanups: delete orphaned `staging`/`develop` branches; remove legacy `OANDA__*` env
  vars; drop prod junk tables (`test`, `trad_history`); delete broken shared
  `Postgres__ConnectionString` if still present; restrict `/metrics`.
- [TECH_DEBT.md](./TECH_DEBT.md): `Program.cs` reads `Redis:ConnectionString` directly
  (IOptions convention violation, low risk).
- An uncommitted draft `.github/workflows/migrate.yml` ("Option B" — GitHub-Actions-driven
  migrations) may exist locally — superseded by the Railway pre-deploy approach; discard.

**History:** [SESSION_LOG.md](./SESSION_LOG.md) is a dated engineering log of every major
change since project start — the best place to understand *why* things are the way they are.
