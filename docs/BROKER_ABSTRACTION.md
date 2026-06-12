# Broker Abstraction (Ports & Adapters)

OANDA is one adapter behind a broker port. A future second broker is a new
adapter + an account-registry row, not a rebuild. This was introduced as a
**pure refactor** — the OANDA adapter produces byte-for-byte the same HTTP
requests the old `OandaClient` produced (pinned by
`TradeFlowGuardian.Tests/OandaBrokerClientMappingTests.cs`).

## The port

`TradeFlowGuardian.Core/Brokers/IBrokerClient.cs` — the only surface through
which the engine talks to a broker:

| Member | Used by |
|---|---|
| `Descriptor` (`BrokerDescriptor`: Name, Leverage) | PositionSizer (margin cap) |
| `PlaceMarketOrderAsync(signal, sl, tp, units)` | SignalExecutionHandler |
| `ClosePositionAsync(instrument)` | SignalExecutionHandler, StatusController |
| `GetAccountBalanceAsync()` | SignalExecutionHandler, StatusController |
| `GetOpenPositionUnitsAsync(instrument)` | SignalExecutionHandler, StatusController |
| `GetMidPriceAsync(instrument)` | PositionSizer (quote→AUD), PriceController |
| `GetPriceSnapshotAsync(instrument)` | PriceController |
| `GetAllOpenPositionsAsync()` | StatusController |
| `GetTransactionsAsync(from, to)` | **nobody yet** — reserved for realised-P&L work; the OANDA adapter throws `NotImplementedException` |

All types crossing the port are Core-owned: `TradeSignal`, `TradeResult`,
`PriceSnapshot`, `OpenPositionSummary`, `BrokerTransaction`. No broker DTOs,
JSON contracts, or broker naming may leak outside an adapter.

## Canonical instrument format

Uppercase `BASE_QUOTE` with an underscore — `EUR_USD`, `USD_JPY`, `GBP_USD`
(OANDA v20 style, the codebase's pre-existing convention). Every instrument
string crossing the port uses this format. Adapters map to/from their broker's
native naming (e.g. a FIX broker using `EURUSD` converts at its own boundary).

## Where leverage lives

`OandaBrokerClient.Descriptor = new("oanda", 30m)` — OANDA AU (ASIC) retail
leverage is 30:1; the v20 API reports 100:1, which is deliberately ignored.
`PositionSizer` computes `marginRate = 1 / Descriptor.Leverage` instead of the
old hardcoded `1/30` constant. The value and the resulting decimal arithmetic
are identical.

## The OANDA adapter

`TradeFlowGuardian.Infrastructure/Brokers/Oanda/OandaBrokerClient.cs`
(renamed/moved from `Infrastructure/Oanda/OandaClient.cs`, method bodies
unchanged). Everything OANDA-specific lives here: v20 endpoint paths, Bearer
auth, FOK time-in-force, 5dp/3dp (JPY) price formatting, signed-units strings,
the close-side `ALL`/`NONE` quirk, and JSON parsing of fill/cancel
transactions.

Credentials resolve per call via `IActiveAccountProvider` (30s cache, Redis
pub/sub invalidation) — unchanged by this refactor.

DI (both Api and Worker `Program.cs`):
`builder.Services.AddHttpClient<IBrokerClient, OandaBrokerClient>();`

## What a new broker adapter must implement

1. A class implementing `IBrokerClient` in
   `TradeFlowGuardian.Infrastructure/Brokers/<Broker>/`, with a truthful
   `Descriptor` (name matches the registry's `broker` column; leverage = the
   regulatory leverage to size against).
2. Instrument mapping to/from the canonical `BASE_QUOTE` format.
3. The same error contract the engine relies on: methods **never throw** for
   transport/API failures — they return `TradeResult.Failed`, `null`, `0m`, or
   an empty list (see existing adapter), because callers treat those as
   recoverable signals.
4. Credential resolution through the account registry (which will need
   broker-aware extension — see follow-ups).
5. Mapping tests pinning its outgoing wire requests, like
   `OandaBrokerClientMappingTests`.

Then: register it in DI keyed off the active account's `broker` value
(selection mechanism is part of the broker-#2 work, not built yet).

## Deferred follow-ups

- `GetTransactionsAsync` OANDA implementation (`/v3/accounts/{id}/transactions`) — lands with realised-P&L work.
- `broker` column (migration `005_broker_column.sql`, default `'oanda'`) exists but is not yet read/written by code; extend `ux_oanda_accounts_account_env` to include `broker` when broker #2 lands.
- Account registry remains OANDA-shaped (`oanda_accounts`, `ActiveOandaAccount`, `environment` fxpractice/fxtrade) — kept to limit blast radius; generalising it is part of broker-#2.
- Backtesting's `IOandaApiService` / `OandaStreamingService` (historical candles, transaction streaming) are outside the live path and outside this port.
