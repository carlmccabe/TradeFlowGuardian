# TradeFlow Guardian

API-native execution engine for the EMAC trading system.
Replaces MT4 EA + VPS with a managed cloud stack at equivalent cost.

## Architecture

```
TradingView Alert (webhook)
        │
        ▼
TradeFlowGuardian.Api          ← ASP.NET Core 10
  • HMAC-SHA256 validation
  • POST /api/signal
  • POST /api/status/close/{instrument}   ← kill switch
  • GET  /api/status/balance
        │
        ▼ (in-memory Channel)
TradeFlowGuardian.Worker       ← .NET Worker Service
  • SignalAgeFilter             ← reject stale alerts
  • AtrSpikeFilter              ← block volatile events (Apr 10 fix)
  • No-pyramiding check
  • Position sizing (mirrors Pine Section 5)
  • SL/TP calculation (ATR × multipliers)
        │
        ▼
OANDA v20 REST API             ← fxpractice / fxtrade
```

## Projects

| Project | Role |
|---|---|
| `TradeFlowGuardian.Core` | Models, interfaces, config — no dependencies |
| `TradeFlowGuardian.Infrastructure` | OANDA client, filters, queue |
| `TradeFlowGuardian.Api` | Webhook receiver, kill switch endpoints |
| `TradeFlowGuardian.Worker` | Background execution loop |

---

## Quick Start

### 1. Prerequisites

- .NET 10 SDK
- OANDA account (practice: https://fxtrade.oanda.com/your_account/fxtrade/register/demo)
- OANDA API key (My Account → Manage API Access)

### 2. Set secrets (never commit these)

```bash
# From TradeFlowGuardian.Api directory
dotnet user-secrets init
dotnet user-secrets set "Oanda:ApiKey" "your-api-key"
dotnet user-secrets set "Oanda:AccountId" "your-account-id"
dotnet user-secrets set "Webhook:Secret" "your-hmac-secret"
```

### 3. Run locally

```bash
# Terminal 1 — API
cd TradeFlowGuardian.Api
dotnet run

# Terminal 2 — Worker
cd TradeFlowGuardian.Worker
dotnet run
```

API will be available at:
- `http://localhost:5000` (HTTP)
- `http://localhost:5000/swagger` (Swagger UI)

### 4. Test the webhook locally

```bash
# Generate HMAC signature
SECRET="your-hmac-secret"
BODY='{"instrument":"USD_JPY","direction":"Long","atr":0.245,"price":149.500,"riskPercent":0,"timestamp":"2026-04-10T00:00:00Z","idempotencyKey":"test_001"}'
SIG=$(echo -n "$BODY" | openssl dgst -sha256 -hmac "$SECRET" | awk '{print $2}')

curl -X POST http://localhost:5000/api/signal \
  -H "Content-Type: application/json" \
  -H "X-Signature: sha256=$SIG" \
  -d "$BODY"
```

---

## TradingView Alert Setup

### Pine alert message template

Add this as your TV alert message (JSON format):

```json
{
  "instrument": "USD_JPY",
  "direction": "{{strategy.order.action == 'buy' ? 'Long' : 'Short'}}",
  "atr": {{plot_0}},
  "price": {{close}},
  "riskPercent": 0,
  "timestamp": "{{timenow}}",
  "idempotencyKey": "{{ticker}}_{{time}}"
}
```

> **Plotting ATR for the webhook**: Add `plot(atr, "ATR")` to your Pine script.
> TV exposes plotted values as `{{plot_0}}`, `{{plot_1}}` etc. in alert messages.

### Webhook URL

```
https://your-domain.com/api/signal
```

### Webhook header (TV Pro/Premium only)

TV doesn't natively sign webhooks — use a secret in the URL as a fallback:

```
https://your-domain.com/api/signal?key=YOUR_SECRET
```

Or upgrade to a TV plan that supports custom headers and pass:

```
X-Signature: sha256=<computed>
```

---

## Configuration Reference

All config lives in `appsettings.json` (or env vars for deployment).

### Oanda

| Key | Default | Notes |
|---|---|---|
| `Oanda:ApiKey` | — | From OANDA dashboard |
| `Oanda:AccountId` | — | Numeric account ID |
| `Oanda:Environment` | `fxpractice` | Change to `fxtrade` for live |

### Risk

| Key | Default | Notes |
|---|---|---|
| `Risk:DefaultRiskPercent` | `1.0` | % of balance risked per trade |
| `Risk:AtrStopMultiplier` | `2.0` | SL = ATR × this |
| `Risk:AtrTargetMultiplier` | `4.0` | TP = ATR × this (2R) |
| `Risk:MaxPositionUnits` | `1000000` | Safety cap |
| `Risk:MaxDailyDrawdownPercent` | `3.0` | Phase 2: circuit breaker |

### Filters

| Key | Default | Notes |
|---|---|---|
| `Filters:EnableAtrSpikeFilter` | `true` | Blocks Apr-10-style whipsaws |
| `Filters:AtrSpikeMultiplier` | `2.0` | Block if ATR > avg × this |
| `Filters:SignalMaxAgeSeconds` | `60` | Reject stale TV alerts |
| `Filters:EnableNewsFilter` | `false` | Phase 2 |
| `Filters:NewsBufferMinutes` | `30` | ±mins around high-impact events |

---

## API Endpoints

### Signal

```
POST /api/signal
Body: TradeSignal JSON
Headers: X-Signature: sha256=<hmac>

Response 202: { message, instrument, direction, queuedAt }
Response 401: Invalid or missing signature
```

### Status

```
GET  /api/status/balance
GET  /api/status/position/{instrument}   e.g. USD_JPY
POST /api/status/close/{instrument}      ← emergency kill switch
```

### Health

```
GET /api/signal/health
GET /                                    ← root ping
```

---

## Deployment (Azure Container Apps)

### Environment variables for production

Set these in Azure Container Apps → Environment Variables:

```
Oanda__ApiKey=<secret>
Oanda__AccountId=<secret>
Oanda__Environment=fxtrade
Webhook__Secret=<secret>
Risk__DefaultRiskPercent=1.0
Filters__EnableAtrSpikeFilter=true
Filters__AtrSpikeMultiplier=2.0
```

### GitHub Actions CI/CD (coming Phase 4)

`.github/workflows/deploy.yml` will build, push to ACR, and redeploy on push to `main`.

---

## Phase Roadmap

- [x] **Phase 1** — Signal ingestion, OANDA client, ATR/age filters, position sizing
- [ ] **Phase 2** — Redis queue + position state, news calendar filter, live FX rate feed
- [ ] **Phase 3** — PostgreSQL trade history, SignalR push, React PWA dashboard
- [ ] **Phase 4** — Docker + GitHub Actions CI/CD, Azure Container Apps deployment
