# TradeFlowGuardian.Worker

.NET Worker Service. Consumes trade signals from the Redis Stream, runs them through the filter chain, sizes and executes positions via the OANDA v20 REST API, and persists every order result to PostgreSQL.

---

## Execution Flow

Each signal is processed in a dedicated DI scope (fresh filter and sizer instances per signal):

```
1. Idempotency check      Redis SetNX on idempotencyKey (24h TTL) — duplicate → drop
2. Close path             Direction == Close → ClosePositionAsync → write history → done
3. Filter chain           All 5 filters run in order (see below)
4. No-pyramiding          Cache-first position check → OANDA fallback on miss → skip if open
5. Balance fetch          GetAccountBalanceAsync → abort if ≤ 0
6. Drawdown check         EnsureDayOpenNavAsync (SetNX, once/day) + CheckAndMarkIfBreachedAsync
7. Position sizing        CalculateUnitsAsync (ATR risk formula, mirrors Pine Section 5)
8. SL / TP calculation    stopLoss = price ± (ATR × AtrStopMultiplier)
                          takeProfit = price ∓ (ATR × AtrTargetMultiplier)
9. Sanity checks          Abort if SL/TP ≤ 0 or on wrong side of entry
10. Place market order     PlaceMarketOrderAsync (CancellationToken.None — never interrupted)
11. Write trade history    ITradeHistoryRepository.InsertAsync (log-and-swallow — never aborts trade)
12. Update position cache  SetAsync on fill, ClearAsync on close
```

Market order calls (`PlaceMarketOrderAsync`, `ClosePositionAsync`) and all post-fill cache + history writes use `CancellationToken.None` — they always run to completion regardless of shutdown signals.

---

## Filter Chain

Filters run in this order. The first blocked result stops evaluation.

| # | Filter | Blocks when |
|---|---|---|
| 1 | `SignalAgeFilter` | Signal timestamp is older than `Filters:SignalMaxAgeSeconds` (default 60s) |
| 2 | `GlobalPauseFilter` | `POST /api/status/pause` has set the Redis pause flag |
| 3 | `DailyDrawdownFilter` | Today's drawdown has exceeded `Risk:MaxDailyDrawdownPercent` of day-open NAV |
| 4 | `AtrSpikeFilter` | Incoming `atr > rollingAverageAtr × Filters:AtrSpikeMultiplier` |
| 5 | `NewsCalendarFilter` | A high-impact economic event is within ±`NewsFilter:BlockWindowMinutes` for either currency in the pair |

All filters are fail-open on external errors (network timeouts, Redis unavailable, etc.) — a filter exception never blocks a trade, it logs a warning and passes.

`Close` direction signals skip all filters and go directly to step 10.

---

## Position Sizing

Mirrors the Pine Script Section 5 risk formula:

```
stopDistance = ATR × AtrStopMultiplier
riskAmount   = accountBalance × (riskPercent / 100)
units        = riskAmount / (stopDistance × fxRate)   ← fxRate converts stop to account currency
units        = min(units, MaxPositionUnits)
units        = negative for Short signals
```

`fxRate` comes from a live OANDA pricing call (`GET /v3/accounts/{id}/pricing`). Conservative hardcoded fallbacks are used only if the API is unreachable.

---

## Trade History

Every order attempt — success and failure, entry and close — is written to the `trade_history` PostgreSQL table after the OANDA call completes.

| Column | Type | Notes |
|---|---|---|
| `id` | `BIGSERIAL` | Auto-assigned primary key |
| `instrument` | `TEXT` | e.g. `USD_JPY` |
| `direction` | `TEXT` | `Long`, `Short`, or `Close` |
| `entry_price` | `NUMERIC(18,5)` | Signal bar price; `0` for Close signals |
| `sl` | `NUMERIC(18,5)?` | Computed stop-loss; `NULL` for Close signals |
| `tp` | `NUMERIC(18,5)?` | Computed take-profit; `NULL` for Close signals |
| `units` | `BIGINT` | Requested units; `0` for Close signals |
| `fill_price` | `NUMERIC(18,5)?` | OANDA fill price; `NULL` on failure |
| `order_id` | `TEXT?` | OANDA order/trade ID; `NULL` on failure |
| `success` | `BOOLEAN` | |
| `error_message` | `TEXT?` | OANDA error or internal reason; `NULL` on success |
| `executed_at` | `TIMESTAMPTZ` | UTC timestamp of the order attempt |

Schema: [`docs/migrations/001_trade_history.sql`](../docs/migrations/001_trade_history.sql)

A write failure is logged and swallowed — it never aborts or masks a trade result.

---

## Observability

The Worker exposes Prometheus metrics on `$PORT` (Railway) or `9091` (local Docker):

| Metric | Type | Labels |
|---|---|---|
| `tradeflow_signals_received_total` | Counter | — |
| `tradeflow_signals_filtered_total` | Counter | `reason` |
| `tradeflow_orders_placed_total` | Counter | `outcome` (`success`/`failed`) |
| `tradeflow_order_latency_seconds` | Histogram | — |
| `tradeflow_account_balance` | Gauge | — |
| `tradeflow_redis_queue_depth` | Gauge | — |

Metrics endpoint: `GET /metrics`  
Grafana dashboard is pre-provisioned in `monitoring/` — see root `docker-compose.yml`.

---

## Local Development

### Docker (recommended)

```bash
./scripts/dev.sh
```

Starts Worker alongside the Api and Redis. Watches source files and rebuilds on changes.

### dotnet run

```bash
cd TradeFlowGuardian.Worker
dotnet user-secrets set "Oanda:ApiKey" "<key>"
dotnet user-secrets set "Oanda:AccountId" "<id>"
dotnet run
```

Requires a local Redis at `localhost:6379` and an accessible Postgres instance (or leave `Postgres:ConnectionString` empty to skip history writes).

---

## Key Configuration

See [docs/CONFIGURATION.md](../docs/CONFIGURATION.md) for the full reference.

| Key | Notes |
|---|---|
| `Oanda:ApiKey` | Required |
| `Oanda:AccountId` | Required |
| `Oanda:Environment` | `fxpractice` or `fxtrade` |
| `Redis:ConnectionString` | Required — shared with Api |
| `Redis:ConsumerName` | Unique per replica — use hostname in production |
| `Postgres:ConnectionString` | Required for trade history persistence |
| `Risk:DefaultRiskPercent` | Default risk per trade |
| `Risk:MaxDailyDrawdownPercent` | Circuit breaker threshold |
| `Risk:AtrStopMultiplier` | Stop distance multiplier |
| `Risk:AtrTargetMultiplier` | Target distance multiplier |
| `Filters:SignalMaxAgeSeconds` | Max signal age before rejection |
| `NewsFilter:Enabled` | Enable/disable news blackout window |
