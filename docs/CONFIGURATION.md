# Configuration Reference — TradeFlow Guardian

All configuration is bound via `IOptions<T>`. Never read `IConfiguration` directly in services.  
Secrets (API keys, passwords) go in environment variables or user-secrets — never in `appsettings.json`.

> **Naming convention for env vars:** replace `:` with `__`  
> e.g. `Oanda:ApiKey` → `Oanda__ApiKey`

---

## Oanda

Bound to `OandaConfig` from section `"Oanda"`.

| Key | Required | Default | Description |
|---|---|---|---|
| `Oanda:ApiKey` | ✅ | — | OANDA REST API key (My Account → Manage API Access) |
| `Oanda:AccountId` | ✅ | — | Numeric account ID shown in OANDA dashboard |
| `Oanda:Environment` | | `fxpractice` | `fxpractice` for paper, `fxtrade` for live |

`BaseUrl` and `StreamUrl` are derived automatically from `Environment` — do not set manually.

---

## Webhook

Bound to `WebhookConfig` from section `"Webhook"`. **Api only.**

| Key | Required | Default | Description |
|---|---|---|---|
| `Webhook:Secret` | ✅ | — | Webhook secret token — append `?secret=<value>` to the TradingView webhook URL |

---

## Redis

Bound to `RedisConfig` from section `"Redis"`. Used by both Api and Worker.

| Key | Required | Default | Description |
|---|---|---|---|
| `Redis:ConnectionString` | ✅ | `localhost:6379` | StackExchange.Redis connection string or Railway `REDIS_URL` |
| `Redis:StreamName` | | `tradeflow:signals` | Redis Stream key shared between Api (writer) and Worker (reader) |
| `Redis:ConsumerGroup` | | `workers` | Consumer group — all Worker replicas share this group |
| `Redis:ConsumerName` | | `worker-1` | Unique per Worker instance; use hostname for multiple replicas |

---

## Postgres

Bound to `PostgresConfig` from section `"Postgres"`. Used by both Api and Worker.

| Key | Required | Default | Description |
|---|---|---|---|
| `Postgres:ConnectionString` | ✅ (for history) | `""` | Npgsql connection string. Empty = history disabled (warning logged). |

Connection string format:
```
Host=<host>;Database=tradeflow;Username=<user>;Password=<password>
```

Schema migrations are plain SQL files in [`docs/migrations/`](migrations/). Run them manually in order before first deploy. No auto-migration runner.

---

## Risk

Bound to `RiskConfig` from section `"Risk"`. Used by Worker.

| Key | Default | Description |
|---|---|---|
| `Risk:DefaultRiskPercent` | `1.0` | % of account balance risked per trade (overridden per-signal if `riskPercent > 0` in payload) |
| `Risk:AtrStopMultiplier` | `2.0` | Stop distance = ATR × this multiplier |
| `Risk:AtrTargetMultiplier` | `4.0` | Target distance = ATR × this multiplier |
| `Risk:MaxPositionUnits` | `1000000` | Hard cap on position size units |
| `Risk:MaxDailyDrawdownPercent` | `3.0` | Daily drawdown circuit breaker threshold (% of day-open NAV) |

Current live settings per pair — see `CLAUDE.md`.

---

## Filters

Bound to `FilterConfig` from section `"Filters"`. Used by Worker.

| Key | Default | Description |
|---|---|---|
| `Filters:SignalMaxAgeSeconds` | `60` | Reject signals older than this many seconds at arrival time |
| `Filters:EnableAtrSpikeFilter` | `true` | Block signals when current ATR > rolling average × multiplier |
| `Filters:AtrSpikeMultiplier` | `2.0` | ATR spike threshold — block if `currentAtr > avgAtr × this` |
| `Filters:EnableNewsFilter` | `false` | Legacy flag (superseded by `NewsFilter:Enabled`) |
| `Filters:NewsBufferMinutes` | `30` | Legacy field — use `NewsFilter:Block*` instead |
| `Filters:EnableSessionFilter` | `false` | Not implemented — reserved |

---

## NewsFilter

Bound to `NewsFilterOptions` from section `"NewsFilter"`. Used by Worker.

| Key | Default | Description |
|---|---|---|
| `NewsFilter:Enabled` | `true` | Enable/disable the ForexFactory news blackout filter |
| `NewsFilter:BlockWindowMinutesBefore` | `30` | Minutes before a high-impact event to block new entries |
| `NewsFilter:BlockWindowMinutesAfter` | `30` | Minutes after a high-impact event to block new entries |
| `NewsFilter:MinimumImpactLevel` | `High` | `Low`, `Medium`, or `High` — events below this level are ignored |
| `NewsFilter:CacheRefreshHours` | `6` | How often to re-fetch the ForexFactory iCal feed |

---

## Dashboard (CORS)

Untyped — read directly at startup in `Api/Program.cs`.

| Key | Default | Description |
|---|---|---|
| `Dashboard:Origin` | `http://localhost:5173` | Allowed CORS origin for the React PWA dashboard |

---

## Logging

Standard Microsoft logging configuration — applies to both Api and Worker.

Recommended production settings (already in `appsettings.json`):

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft": "Warning",
    "Microsoft.Hosting.Lifetime": "Information",
    "StackExchange": "Warning",
    "System.Net.Http.HttpClient": "Warning"
  }
}
```

In `Production` environment, both services switch to single-line console output for Railway log streaming.
