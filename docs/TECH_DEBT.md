# Tech Debt — TradeFlow Guardian

## Current Issues

### Configuration
- [x] ~~Worker appsettings.json incomplete~~ — Fixed 2026-04-10: Added Oanda/Risk/Filters sections to match Api structure

### Docker
- [ ] **Dockerfiles may reference .NET 8** — need verification they target .NET 10 SDK/runtime
  - Impact: Docker builds may fail or use wrong framework
  - Fix: Check and update base images to `mcr.microsoft.com/dotnet/sdk:10.0` and `mcr.microsoft.com/dotnet/aspnet:10.0`
  - Priority: Medium

### Architecture
- [ ] **In-memory queue doesn't work across containers** — docker-compose runs Api + Worker in separate containers, but InMemorySignalQueue uses Channel<T> which is in-process only
  - Impact: Signals queued in Api won't reach Worker
  - Fix: Phase 2 — migrate to Redis Streams
  - Workaround: Run both in same process for local dev (use `dotnet run` from Api with Worker as hosted service)
  - Priority: Low (known limitation, deferred to Phase 2)

### Code Quality
- ~~No open items~~

## Resolved
- [x] ~~TradeResult.Succeeded doesn't support Message property~~ — Fixed 2026-04-10: Use object initializer in ClosePositionAsync
- [x] ~~OpenApi package compatibility issue~~ — Fixed 2026-04-10: Removed Microsoft.AspNetCore.OpenApi, kept Swashbuckle only
- [x] ~~System.Text.Json unnecessary package reference~~ — Fixed 2026-04-10: Removed from Infrastructure project
- [x] ~~Projects on .NET 8~~ — Migrated all to .NET 10 2026-04-10
- [x] ~~Hardcoded FX rates in PositionSizer~~ — Fixed 2026-04-10: `GetMidPriceAsync` added to `IOandaClient`/`OandaClient`; `PositionSizer` calls live pricing endpoint with conservative fallbacks on failure

---

## Process
- Add new tech debt items as `[ ]` under "Current Issues"
- Move to "Resolved" with `[x]` when fixed, include date and one-line summary
- Reference high-priority items in CLAUDE.md "What NOT To Do" if they're constraints
- Review quarterly or before major releases
