# Deployment — TradeFlow Guardian

## Platform

Railway. Two services, one shared Redis plugin, same GitHub repo.

```
GitHub repo
    ├── Api service      (public URL — receives TradingView webhooks)
    ├── Worker service   (no public URL — consumes Redis Stream, executes trades)
    └── Redis plugin     (shared by both services)
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
| `ASPNETCORE_ENVIRONMENT` | `Production` | Enables single-line logging, hides Swagger |
| `Oanda__ApiKey` | `<your key>` | Double underscore — .NET config binding |
| `Oanda__AccountId` | `<your account id>` | |
| `Oanda__Environment` | `fxpractice` | Change to `fxtrade` for live |
| `Webhook__Secret` | `<your secret>` | Append `?secret=<value>` to the webhook URL in TradingView |
| `Redis__ConnectionString` | From Redis plugin | See below |
| `Postgres__ConnectionString` | From Postgres plugin | See below |
| `Dashboard__Origin` | `https://<your-dashboard-url>` | CORS — omit if no dashboard yet |

**Worker service:**

| Variable | Value | Notes |
|---|---|---|
| `DOTNET_ENVIRONMENT` | `Production` | |
| `Oanda__ApiKey` | Same as Api | |
| `Oanda__AccountId` | Same as Api | |
| `Oanda__Environment` | `fxpractice` | |
| `Redis__ConnectionString` | From Redis plugin | Same instance as Api |
| `Redis__ConsumerName` | `worker-1` | Change if running multiple Worker replicas |
| `Postgres__ConnectionString` | From Postgres plugin | Same instance as Api |

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
