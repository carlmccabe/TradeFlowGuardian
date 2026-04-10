# Tech Debt — TradeFlow Guardian

## Current Issues

### Configuration
- [x] ~~Worker appsettings.json incomplete~~ — Fixed 2026-04-10: Added Oanda/Risk/Filters sections to match Api structure

### Docker
~~No open items~~

### Architecture
~~No open items~~

### Code Quality
- ~~No open items~~

## Resolved
- [x] ~~TradeResult.Succeeded doesn't support Message property~~ — Fixed 2026-04-10: Use object initializer in ClosePositionAsync
- [x] ~~OpenApi package compatibility issue~~ — Fixed 2026-04-10: Removed Microsoft.AspNetCore.OpenApi, kept Swashbuckle only
- [x] ~~System.Text.Json unnecessary package reference~~ — Fixed 2026-04-10: Removed from Infrastructure project
- [x] ~~Projects on .NET 8~~ — Migrated all to .NET 10 2026-04-10
- [x] ~~Dockerfiles reference .NET 8~~ — Confirmed .NET 10 base images in use 2026-04-11
- [x] ~~In-memory queue doesn't work across containers~~ — Resolved 2026-04-11: Migrated to Redis Streams (Phase 2)
- [x] ~~No position state cache~~ — Resolved 2026-04-11: `RedisPositionCache` (IPositionCache) added; write-through with 5-min TTL; SignalExecutionHandler uses cache-first with OANDA fallback
- [x] ~~Hardcoded FX rates in PositionSizer~~ — Fixed 2026-04-10: `GetMidPriceAsync` added to `IOandaClient`/`OandaClient`; `PositionSizer` calls live pricing endpoint with conservative fallbacks on failure

---

## Process
- Add new tech debt items as `[ ]` under "Current Issues"
- Move to "Resolved" with `[x]` when fixed, include date and one-line summary
- Reference high-priority items in CLAUDE.md "What NOT To Do" if they're constraints
- Review quarterly or before major releases
