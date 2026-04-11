# TradeFlowGuardian.Api

ASP.NET Core 10 webhook receiver. Validates incoming TradingView alerts, queues them to Redis Streams, and exposes status and kill-switch endpoints.

The Api **never executes trades** — it validates and queues only. All trading logic lives in the Worker.

---

## Endpoints

### Signal

```
POST /api/signal
```

Receives a TradingView webhook alert. Validates the HMAC-SHA256 signature, deserialises the payload, and pushes it to the Redis Stream.

- **Auth:** `X-Signature: sha256=<hmac>` header required
- **Response 202:** signal queued
- **Response 400:** malformed payload
- **Response 401:** missing or invalid signature
- **Response 429:** queue full

### Status

```
GET  /api/status/balance
```
Returns current OANDA account balance.

```
GET  /api/status/position/{instrument}
```
Returns open position units for one instrument (e.g. `USD_JPY`). Empty if flat.

```
GET  /api/status/positions
```
Returns all open positions: instrument, units, unrealised P&L, average price.

```
GET  /api/status/filters
```
Returns current system-level filter state:
```json
{
  "paused": false,
  "dailyDrawdown": {
    "isBreached": false,
    "dayOpenNav": 10500.00,
    "currentBalance": 10665.78,
    "drawdownPercent": -1.57,
    "maxDrawdownPercent": 3.0,
    "tradingDay": "20260411"
  }
}
```

```
GET  /api/status/price/{instrument}
```
Returns live mid-price from OANDA for one instrument.

```
POST /api/status/close/{instrument}
```
Emergency kill switch — closes the open position for the specified instrument immediately.

```
POST /api/status/pause
Body: { "paused": true }
```
Globally pauses or resumes new Long/Short entries. Persisted in Redis — survives Worker restarts.

### Health

```
GET /
```
Root ping — returns service name, version, and UTC timestamp.

```
GET /metrics
```
Prometheus metrics endpoint (internal network only — not HMAC protected).

---

## Signal Payload

```json
{
  "instrument": "USD_JPY",
  "direction": "Long",
  "atr": 0.245,
  "price": 149.500,
  "riskPercent": 0,
  "timestamp": "2026-04-11T00:00:00Z",
  "idempotencyKey": "USDJPY_1744329600"
}
```

| Field | Type | Notes |
|---|---|---|
| `instrument` | string | OANDA format: `USD_JPY`, `EUR_USD`, `GBP_USD` |
| `direction` | string | `"Long"`, `"Short"`, or `"Close"` |
| `atr` | decimal | ATR value at signal bar close — used by AtrSpikeFilter |
| `price` | decimal | Bar close price — used for SL/TP calculation |
| `riskPercent` | decimal | Per-trade risk override. `0` = use server default |
| `timestamp` | ISO 8601 UTC | Signal generation time — used by SignalAgeFilter |
| `idempotencyKey` | string? | Unique key to prevent duplicate execution (recommended: `{ticker}_{time}`) |

---

## HMAC Authentication

Every `POST /api/signal` request must include:

```
X-Signature: sha256=<hex digest>
```

Where the digest is `HMAC-SHA256(secret, raw request body)`.

`secret` must match `Webhook:Secret` in the Api configuration.

### Generating the signature manually (testing)

```bash
SECRET="your-webhook-secret"
BODY='{"instrument":"USD_JPY","direction":"Long","atr":0.245,"price":149.500,"riskPercent":0,"timestamp":"2026-04-11T00:00:00Z","idempotencyKey":"test_001"}'
SIG=$(echo -n "$BODY" | openssl dgst -sha256 -hmac "$SECRET" | awk '{print $2}')

curl -X POST https://<api-url>/api/signal \
  -H "Content-Type: application/json" \
  -H "X-Signature: sha256=$SIG" \
  -d "$BODY"
```

Or use [`scripts/test-signal.sh`](../scripts/test-signal.sh) which handles this automatically.

### TradingView alert setup

In TradingView alert → **Notifications → Webhook URL**:
```
https://<your-api-url>/api/signal
```

Alert message body:
```json
{
  "instrument": "USD_JPY",
  "direction": "{{strategy.order.action}}",
  "atr": {{plot("ATR")}},
  "price": {{close}},
  "riskPercent": 0,
  "timestamp": "{{timenow}}",
  "idempotencyKey": "{{exchange}}_{{ticker}}_{{time}}"
}
```

Add `plot(atr_value, "ATR")` to your Pine script so TV exposes it via `{{plot("ATR")}}`.

---

## Local Development

### Docker (recommended)

```bash
./scripts/dev.sh
```

Starts Api on `http://localhost:8080`, Worker, and Redis together.

### dotnet run

```bash
cd TradeFlowGuardian.Api
dotnet user-secrets set "Oanda:ApiKey" "<key>"
dotnet user-secrets set "Oanda:AccountId" "<id>"
dotnet user-secrets set "Webhook:Secret" "<secret>"
dotnet run
```

Swagger UI: `http://localhost:5205/swagger`

---

## Key Configuration

See [docs/CONFIGURATION.md](../docs/CONFIGURATION.md) for the full reference.

| Key | Notes |
|---|---|
| `Oanda:ApiKey` | Required |
| `Oanda:AccountId` | Required |
| `Oanda:Environment` | `fxpractice` or `fxtrade` |
| `Webhook:Secret` | Required — must match TradingView |
| `Redis:ConnectionString` | Required — shared with Worker |
| `Postgres:ConnectionString` | Required for history endpoint (future) |
| `Dashboard:Origin` | CORS origin for the React PWA |
