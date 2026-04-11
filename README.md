# TradeFlow Guardian

API-native forex trade execution engine. Receives TradingView webhook alerts, applies risk filters, and executes via the OANDA v20 REST API. Replaces an MT4 EA + VPS with a managed cloud stack on Railway.

## Architecture

```
TradingView Alert (webhook POST)
         │
         ▼
 TradeFlowGuardian.Api           ASP.NET Core 10
   HMAC-SHA256 validation
   POST /api/signal
   GET  /api/status/*
   POST /api/status/pause         global kill switch
         │
         ▼  Redis Stream (tradeflow:signals)
 TradeFlowGuardian.Worker        .NET Worker Service
   SignalAgeFilter
   GlobalPauseFilter
   DailyDrawdownFilter            circuit breaker
   AtrSpikeFilter
   NewsCalendarFilter             ForexFactory iCal
   No-pyramiding check
   Position sizing (ATR risk formula)
   SL / TP calculation
         │                              │
         ▼                              ▼
 OANDA v20 REST API           PostgreSQL (trade_history)
 fxpractice / fxtrade         Npgsql + Dapper
                               written after every order
```

## Projects

| Project | Role |
|---|---|
| `TradeFlowGuardian.Core` | Models, interfaces, config — zero dependencies |
| `TradeFlowGuardian.Infrastructure` | OANDA client, filters, queue, cache, history repository |
| `TradeFlowGuardian.Api` | Webhook receiver, status + kill-switch endpoints |
| `TradeFlowGuardian.Worker` | Background execution loop, signal handler |
| `TradeFlowGuardian.Dashboard` | React 18 PWA — balance, positions, filter status, kill switch |
| `TradeFlowGuardian.Tests` | xUnit + Moq unit tests |

## Quick Start (local Docker)

```bash
# One-time: store secrets in macOS Keychain (triggers system dialog on every read)
./scripts/setup-secrets.sh

# Daily driver — pulls secrets from Keychain, builds and watches for changes
./scripts/dev.sh                # Api + Worker + Redis
./scripts/dev.sh --full         # + Grafana + Prometheus + Loki + RedisInsight
./scripts/dev.sh --down         # stop everything
./scripts/dev.sh --logs         # tail all logs
```

API → `http://localhost:8080`  
Swagger → `http://localhost:8080/swagger`  
Dashboard → `http://localhost:5173` (`cd TradeFlowGuardian.Dashboard && npm run dev`)

### Without Docker

```bash
# Terminal 1
cd TradeFlowGuardian.Api && dotnet run

# Terminal 2
cd TradeFlowGuardian.Worker && dotnet run
```

Requires a local Redis at `localhost:6379`.

## Documentation

| Doc | Contents |
|---|---|
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | Railway setup, env vars, smoke tests |
| [docs/CONFIGURATION.md](docs/CONFIGURATION.md) | All `appsettings.json` keys and defaults |
| [docs/SECRETS.md](docs/SECRETS.md) | Keychain ACL (dev) → 1Password → Azure Key Vault |
| [docs/migrations/](docs/migrations/) | PostgreSQL schema SQL — run manually in order |
| [docs/TECH_DEBT.md](docs/TECH_DEBT.md) | Known issues and resolutions |
| [docs/SESSION_LOG.md](docs/SESSION_LOG.md) | Change log by session |
| [TradeFlowGuardian.Api/README.md](TradeFlowGuardian.Api/README.md) | Endpoints, signal payload, HMAC |
| [TradeFlowGuardian.Worker/README.md](TradeFlowGuardian.Worker/README.md) | Filter chain, execution flow, position sizing |

## Roadmap

- [x] **Phase 1** — OANDA client, HMAC webhook, ATR/age filters, position sizing
- [x] **Phase 2** — Redis Streams queue, position cache, news filter, drawdown circuit breaker, live FX rates, PostgreSQL trade history
- [ ] **Phase 3** — P&L chart, SignalR real-time push
- [ ] **Phase 4** — GitHub Actions CI/CD, Cloudflare DNS
