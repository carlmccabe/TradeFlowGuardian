# Deployment — TradeFlow Guardian

## Platform

Railway. Two environments (production + staging), each with the same service
topology but fully isolated plugins.

```
GitHub repo (carlmccabe/tradeflowguardian)
    │
    ├── main branch   → Railway production environment
    │       ├── Api service      (public URL — receives TradingView webhooks)
    │       ├── Worker service   (no public URL — consumes Redis Stream, executes trades)
    │       ├── Redis plugin     (production only)
    │       └── PostgreSQL plugin (production only)
    │
    └── develop branch → Railway staging environment
            ├── Api service      (separate public URL)
            ├── Worker service
            ├── Redis plugin     (staging only — isolated from production)
            └── PostgreSQL plugin (staging only — isolated from production)
```

---

## Environments & Branching

Two Railway environments, each with fully isolated services and plugins:

```
GitHub branch       Railway environment    OANDA account
─────────────────   ────────────────────   ─────────────
main            →   production             fxpractice (or fxtrade for live)
develop         →   staging                fxpractice only
```

### Branch → environment mapping (Railway dashboard)

Railway does not read the branch setting from `railway.toml` — it is configured
per-service in the Railway dashboard. The `railway.toml` files in this repo are
environment-agnostic (build + deploy settings only).

**Steps to configure staging to watch `develop`:**

1. In Railway dashboard → project → click the environment dropdown (top of page)
   and select **staging**.
2. For each service (**Api**, **Worker**, **Dashboard**) in the staging environment:
   - Click the service → **Settings** tab → **Source** section
   - Change **Branch** from `main` → `develop`
   - Click **Save** — Railway will redeploy from `develop` immediately
3. The **production** environment services should already be set to `main`.
   Confirm: Settings → Source → Branch = `main` for each service.

### Service isolation

When Railway clones an environment it creates separate plugin instances.
Staging must **never** share Redis or Postgres with production.

To verify isolation in Railway dashboard:
- staging environment → Redis plugin → **Connect** — note the hostname
- production environment → Redis plugin → **Connect** — hostname must differ

If the hostnames are the same the staging environment is sharing production data —
add a new Redis plugin to the staging environment and update
`Redis__ConnectionString` in both staging services.

Same check applies to the PostgreSQL plugin.

### Env vars that differ between environments

Set these in the Railway dashboard per environment. All other variables (OANDA
credentials, risk parameters, filter settings) can be identical if both
environments target the same OANDA practice account.

| Variable | production | staging | Notes |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Staging` | Controls .NET config loading; `Staging` shows Swagger UI |
| `DOTNET_ENVIRONMENT` | `Production` | `Staging` | Worker equivalent of `ASPNETCORE_ENVIRONMENT` |
| `ENVIRONMENT` | `production` | `staging` | Plain tag — for log filtering and future trade-history tagging |
| `Webhook__Secret` | prod secret | staging secret | Must differ — prevents staging from accepting production TV alerts |
| `Redis__ConnectionString` | prod Redis URL | staging Redis URL | From each environment's own Redis plugin |
| `Postgres__ConnectionString` | prod Postgres URL | staging Postgres URL | From each environment's own Postgres plugin |
| `Dashboard__Origin` | prod dashboard URL | staging dashboard URL | CORS — update when staging dashboard has its own URL |

Variables that should be **identical** across both environments unless you are
running separate OANDA accounts:

```
Oanda__ApiKey
Oanda__AccountId
Oanda__Environment      # always fxpractice for staging
Redis__ConsumerGroup    # workers
Redis__ConsumerName     # worker-1
```

### Promoting staging to production

1. Open a pull request on GitHub: **`develop` → `main`**
2. Review, merge — Railway auto-deploys production from `main`
3. If any new files exist in `docs/migrations/` that haven't been run against
   production Postgres, run them now (see "Running the schema migration" below)
4. Smoke-test production endpoints (health, balance, db status)

Never push directly to `main`. All changes go through `develop` first.

### Running migrations per environment

Each environment's Postgres is independent. Migrations must be run separately:

```bash
# staging
railway environment staging
railway connect PostgreSQL
# paste docs/migrations/001_trade_history.sql etc.

# production
railway environment production
railway connect PostgreSQL
# paste the same files
```

---

## Prerequisites

- Railway account with project created
- Redis plugin added to the project (Railway dashboard → + New → Database → Redis)
- PostgreSQL plugin added to the project (Railway dashboard → + New → Database → PostgreSQL)
- OANDA practice account with API key and account ID
- TradingView webhook secret (any strong random string, e.g. `openssl rand -hex 32`)

---

## First-Time Setup

### 1. Add the Api service

In Railway dashboard:

| Setting | Value |
|---|---|
| Source | GitHub → `carlmccabe/tradeflowguardian` |
| Root Directory | `/` (repo root) |
| Build | Auto-detected from `railway.toml` |
| Public domain | Yes — this is your TradingView webhook URL |

Railway picks up `railway.toml` at the repo root automatically:
- Dockerfile: `TradeFlowGuardian.Api/Dockerfile`
- Build context: `.` (repo root — required for sibling project COPY)
- Health check: `GET /`

### 2. Add the Worker service

| Setting | Value |
|---|---|
| Source | Same GitHub repo |
| Root Directory | `TradeFlowGuardian.Worker` |
| Build | Auto-detected from `TradeFlowGuardian.Worker/railway.toml` |
| Public domain | **None** — Worker has no public endpoints |

Railway picks up `TradeFlowGuardian.Worker/railway.toml`:
- Dockerfile: `Dockerfile` (relative to Worker root)
- Build context: `..` (repo root)
- Health check: `GET /metrics` (KestrelMetricServer on `$PORT`)

### 3. Set environment variables

**Api service:**

| Variable | Value | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (prod) / `Staging` (staging) | Controls .NET config; `Staging` shows Swagger |
| `ENVIRONMENT` | `production` / `staging` | Plain tag for log filtering |
| `Oanda__ApiKey` | `<your key>` | Double underscore — .NET config binding |
| `Oanda__AccountId` | `<your account id>` | |
| `Oanda__Environment` | `fxpractice` | Change to `fxtrade` for live production only |
| `Webhook__Secret` | `<your secret>` | Append `?secret=<value>` to the webhook URL in TradingView. Use different values per environment. |
| `Redis__ConnectionString` | From Redis plugin | Must be the environment's own Redis plugin URL |
| `Postgres__ConnectionString` | From Postgres plugin | Must be the environment's own Postgres plugin URL |
| `Dashboard__Origin` | `https://<your-dashboard-url>` | CORS — omit if no dashboard yet |

**Worker service:**

| Variable | Value | Notes |
|---|---|---|
| `DOTNET_ENVIRONMENT` | `Production` (prod) / `Staging` (staging) | |
| `ENVIRONMENT` | `production` / `staging` | Plain tag for log filtering |
| `Oanda__ApiKey` | Same as Api | |
| `Oanda__AccountId` | Same as Api | |
| `Oanda__Environment` | `fxpractice` | |
| `Redis__ConnectionString` | From Redis plugin | Same instance as Api, per environment |
| `Redis__ConsumerName` | `worker-1` | Change if running multiple Worker replicas |
| `Postgres__ConnectionString` | From Postgres plugin | Same instance as Api, per environment |

**Getting the Redis connection string:**

Railway dashboard → Redis plugin → Connect → copy `REDIS_URL`.
It looks like: `redis://default:password@hostname.railway.internal:6379`

Paste that value as `Redis__ConnectionString` in both services.

**Getting the Postgres connection string:**

Railway dashboard → PostgreSQL plugin → Connect → copy the connection string.
It looks like: `postgresql://postgres:password@hostname.railway.internal:5432/railway`

Convert to Npgsql format and paste as `Postgres__ConnectionString` in both services:
```
Host=hostname.railway.internal;Database=railway;Username=postgres;Password=<password>
```

**Running the schema migration:**

After the Postgres plugin is up, connect to it and run:

```bash
# Via Railway CLI
railway connect PostgreSQL

# Then paste the contents of:
# docs/migrations/001_trade_history.sql
```

Or use any Postgres client (TablePlus, psql) pointed at the Railway connection details. Run each file in `docs/migrations/` in numerical order. Only needs to be done once per environment.

---

## Confirming a Healthy Deploy

After both services deploy, check Railway logs for the startup banners:

**Api:**
```
HH:mm:ss info: Program[0] API starting | OANDA=fxpractice | Url=https://api-fxpractice.oanda.com | Redis=hostname.railway.internal:6379 | Stream=tradeflow:signals | AtrFilter=True | NewsFilter=True | Cors=...
```

**Worker:**
```
HH:mm:ss info: Program[0] Worker starting | OANDA=fxpractice | Url=https://api-fxpractice.oanda.com | Redis=hostname.railway.internal:6379 | Stream=tradeflow:signals | Consumer=worker-1 | AtrFilter=True | NewsFilter=True
HH:mm:ss info: ExecutionWorker[0] ExecutionWorker started — waiting for signals
```

If Redis shows `localhost:6379`, the `Redis__ConnectionString` env var wasn't picked up.
If the OANDA URL is wrong, check `Oanda__Environment` (double underscore).

**Quick smoke test:**
```bash
curl https://<your-api-url>/api/signal/health
# → {"status":"ok","utc":"..."}

curl https://<your-api-url>/api/status/balance
# → {"balanceAud":10665.78,"fetchedAt":"..."}

curl https://<your-api-url>/api/status/db
# → {"reachable":true,"rowCount":0,"error":null,"checkedAt":"..."}
# reachable:false means the connection string is wrong or the migration hasn't been run
```


---

## TradingView Webhook Setup

1. In TradingView alert → Notifications → Webhook URL:
   ```
   https://<your-api-url>/api/signal
   ```

2. Append your secret to the URL:
   ```
   https://<your-api-url>/api/signal?secret=<your-secret>
   ```
   Where `<your-secret>` matches `Webhook__Secret` in Railway.

3. Alert message body: **leave blank** (or set to `{}`).

   The Pine Script in `pine/usdjpy_emac_signal.pine` uses `alert()` calls with
   `str.format()` to embed runtime values (ATR, calculated SL/TP, entry price)
   directly in the JSON payload. Anything typed in the TV message box overrides
   the `alert()` payload and breaks field mapping — the message box can only
   interpolate built-in TV placeholders like `{{close}}`, not custom Pine Script
   variables.

   Example payload sent by the Pine Script `alert()` call:
   ```json
   {
     "instrument": "USD_JPY",
     "direction": "Long",
     "price": 149.523,
     "atr": 0.245,
     "stopLoss": 148.886,
     "takeProfit": 150.821,
     "riskPercent": 0,
     "idempotencyKey": "USD_JPY_1744329600000_L"
   }
   ```
   Direction values: `Long` | `Short` | `Close`

   When `stopLoss` and `takeProfit` are both `> 0` the Worker uses them
   directly and skips server-side ATR re-calculation. `price` and `atr` are
   still included for logging and ATR spike filter evaluation.

---

## Redeployment

Railway auto-deploys on push to the configured branch. To force a manual redeploy:
Railway dashboard → service → Deployments → Redeploy.

**Shutdown behaviour on redeploy:**
Both services have a 30-second graceful shutdown window (matches Railway's
SIGTERM-to-SIGKILL grace period). The Worker logs one of:
- `ExecutionWorker idle at shutdown — no signal in flight` (most common)
- `ExecutionWorker stopped cleanly` (after draining an in-flight signal)

Market orders (`PlaceMarketOrderAsync`) and position closes (`ClosePositionAsync`)
use `CancellationToken.None` — they will always run to completion even if shutdown
is requested mid-flight.

---

## Environment Variables Reference (complete)

All optional variables have safe defaults in `appsettings.json`. Only the
required ones will prevent startup if missing.

| Variable | Required | Default | Description |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | | `Production` | .NET environment name — use `Staging` in staging environment |
| `DOTNET_ENVIRONMENT` | | `Production` | Worker equivalent — use `Staging` in staging environment |
| `ENVIRONMENT` | | — | Plain tag (`production` / `staging`) for log filtering and future trade-history tagging |
| `Oanda__ApiKey` | ✅ | — | OANDA API key |
| `Oanda__AccountId` | ✅ | — | OANDA account ID |
| `Oanda__Environment` | | `fxpractice` | `fxpractice` or `fxtrade` |
| `Webhook__Secret` | ✅ (Api) | — | Webhook secret token — append `?secret=<value>` to the webhook URL |
| `Redis__ConnectionString` | ✅ | `localhost:6379` | Railway Redis URL |
| `Redis__StreamName` | | `tradeflow:signals` | Redis Stream key |
| `Postgres__ConnectionString` | ✅ | `""` | Railway Postgres URL (Npgsql format). Empty disables history writes (warning logged). |
| `Redis__ConsumerGroup` | | `workers` | Shared across all Worker replicas |
| `Redis__ConsumerName` | | `worker-1` | Unique per Worker instance |
| `Dashboard__Origin` | | `http://localhost:5173` | CORS origin for dashboard |
| `Risk__DefaultRiskPercent` | | `1.0` | Default risk % per trade |
| `Risk__AtrStopMultiplier` | | `2.0` | ATR × this = stop distance |
| `Risk__AtrTargetMultiplier` | | `4.0` | ATR × this = target distance |
| `Filters__EnableAtrSpikeFilter` | | `true` | Block signals during ATR spikes |
| `Filters__AtrSpikeMultiplier` | | `2.0` | ATR spike threshold multiplier |
| `Filters__SignalMaxAgeSeconds` | | `60` | Reject signals older than this |
| `NewsFilter__Enabled` | | `true` | Block signals near high-impact news |
| `NewsFilter__BlockWindowMinutesBefore` | | `30` | Minutes before event to block |
| `NewsFilter__BlockWindowMinutesAfter` | | `30` | Minutes after event to block |

---

## Secrets

See [docs/SECRETS.md](./SECRETS.md) for local dev (macOS Keychain) and
go-live secret management strategy.

**Railway-specific:** secrets are set as service environment variables in the
Railway dashboard. They are encrypted at rest and injected at container startup.
Never commit them — `appsettings.json` uses placeholder values only.
