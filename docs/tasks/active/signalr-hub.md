# Task: SignalR Hub (replace dashboard polling)

**Branch:** `develop`
**BACKLOG status to set:** `active` when you start, `done` when merged
**Prerequisite:** Do this task after `pnl-chart.md` is merged — not because of a code
dependency, but to avoid merge conflicts on `App.tsx` and `client.ts`.

---

## What to build

Replace the dashboard's interval polling (`usePolling`) with a SignalR connection
for real-time balance and position updates pushed from the API. Polling was the
Phase 3 interim bridge; this is Phase 3 final.

The SignalR hub lives in `TradeFlowGuardian.Api`. The Worker pushes updates to the
hub after every order fill or position change. The dashboard subscribes and updates
state on each push.

---

## Context

Read `CLAUDE.md` and `docs/tasks/AGENT_INSTRUCTIONS.md` first.

### Current polling setup (to be replaced)

- `TradeFlowGuardian.Dashboard/src/hooks/usePolling.ts` — generic interval poller
- `BalanceWidget.tsx` polls `GET /api/status/balance` every 10 s
- `PositionsPanel.tsx` polls `GET /api/status/positions` every 5 s
- `FilterStatus.tsx` polls `GET /api/status/filters` every 10 s

Polling in `BalanceWidget` and `PositionsPanel` should be replaced with SignalR.
`FilterStatus` polling can stay (filter state rarely changes, push is overkill).

### Backend changes

**1. Add SignalR package to Api:**
```xml
<!-- TradeFlowGuardian.Api/TradeFlowGuardian.Api.csproj -->
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="..." />
```
ASP.NET Core 10 includes SignalR in the framework — no extra package needed.
Use `builder.Services.AddSignalR()` and `app.MapHub<TradeHub>("/hubs/trade")`.

**2. Create the hub:**
```
TradeFlowGuardian.Api/Hubs/TradeHub.cs
```
The hub broadcasts two message types:
- `BalanceUpdated` — `{ balanceAud: decimal, fetchedAt: DateTimeOffset }`
- `PositionsUpdated` — `{ positions: OpenPositionSummary[] }`

**3. Create `ITradeHubPusher` interface in Core:**
```
TradeFlowGuardian.Core/Interfaces/ITradeHubPusher.cs
```
```csharp
public interface ITradeHubPusher
{
    Task PushBalanceAsync(decimal balance, CancellationToken ct = default);
    Task PushPositionsAsync(IReadOnlyList<OpenPositionSummary> positions, CancellationToken ct = default);
}
```

**4. Implement in Infrastructure (or Api):**
`SignalRTradeHubPusher` — injects `IHubContext<TradeHub>`, calls
`Clients.All.SendAsync(...)`.

**5. Wire in Worker:**
After a successful order fill, the Worker should push updated balance and positions.
In `SignalExecutionHandler`, inject `ITradeHubPusher` and call after `PlaceMarketOrderAsync`.

Problem: Worker and Api are separate processes — they don't share the same SignalR hub.
**Solution: The Worker calls the API's push endpoint via HTTP.**

Add `POST /api/hub/push/balance` and `POST /api/hub/push/positions` internal endpoints
to the Api, protected by the same `Webhook__Secret` param. Worker calls these after
fills using an `HttpClient`.

Or (simpler): Have the dashboard fall back to polling after 30 s without a push.
Keep polling as fallback, add SignalR as the fast path.

**Recommended approach:** Keep `usePolling` as fallback; add a `useSignalR` hook that,
when connected, cancels the polling interval. If SignalR disconnects, polling resumes.
The Worker continues to not know about SignalR — pushes happen when the API receives
a new signal response (the Api already gets the 202 Accepted callback from the queue).

Actually, the cleanest minimal scope: **push from the API only when it receives a signal**
(after enqueuing). Balance/positions are still polled. The push carries the signal metadata
so the dashboard can show a "signal received" notification. Full real-time P&L can be a
follow-on task.

**Clarified scope for this task:**
1. API gets a SignalR hub
2. When `POST /api/signal` is received, hub broadcasts `SignalReceived` event with instrument + direction
3. Dashboard shows a toast/banner when a signal is received (no polling replacement yet for balance/positions)
4. `usePolling` stays for balance and positions — do not remove it
5. This unblocks Phase 3 "SignalR hub" checkbox in CLAUDE.md without the cross-process complexity

---

## Files to read before starting

| File | Why |
|---|---|
| `TradeFlowGuardian.Api/Program.cs` | Add SignalR services and MapHub here |
| `TradeFlowGuardian.Api/Controllers/SignalController.cs` | Inject hub pusher here |
| `TradeFlowGuardian.Core/Interfaces/IServices.cs` | Add ITradeHubPusher here |
| `TradeFlowGuardian.Dashboard/src/App.tsx` | Mount toast/notification here |
| `TradeFlowGuardian.Dashboard/src/api/client.ts` | SignalR connection setup here |
| `TradeFlowGuardian.Dashboard/package.json` | Add @microsoft/signalr here |
| `docs/DEPLOYMENT.md` | CORS origins — SignalR needs WebSocket allowed |

---

## Acceptance criteria

- [ ] `builder.Services.AddSignalR()` and `app.MapHub<TradeHub>("/hubs/trade")` added to Api
- [ ] `TradeHub.cs` exists in `TradeFlowGuardian.Api/Hubs/`
- [ ] `SignalController` broadcasts `SignalReceived` on every successfully queued signal
- [ ] CORS policy updated to allow WebSocket upgrade from `Dashboard__Origin`
- [ ] `@microsoft/signalr` added to Dashboard package.json
- [ ] `useSignalR.ts` hook created in `src/hooks/` — connects to `/hubs/trade`, subscribes to `SignalReceived`
- [ ] A non-blocking toast or status strip in the dashboard shows the last received signal (instrument + direction + timestamp)
- [ ] Toast auto-dismisses after 10 s
- [ ] Hub gracefully handles connection drops (SignalR auto-reconnect enabled)
- [ ] Existing `usePolling` for balance/positions is **not removed**
- [ ] `dotnet build TradeFlowGuardian.sln` clean
- [ ] `npm run build` succeeds

---

## Out of scope

- Replacing balance/position polling with SignalR push (follow-on task)
- Worker-to-hub direct push (requires cross-process transport like Redis backplane)
- Redis SignalR backplane
- Authentication on the hub (the hub endpoint is read-only push; no user auth needed now)

---

## Gotchas

- CORS for SignalR requires `AllowCredentials()` + explicit origin (no wildcard).
  The existing CORS policy in `Api/Program.cs` already uses `WithOrigins(...)` and
  `AllowCredentials()` — just confirm the `Dashboard__Origin` value covers the staging URL.
- SignalR WebSocket upgrade requires the CORS pre-flight to succeed. Test with the
  staging dashboard URL before calling it done.
- ASP.NET Core 10 includes SignalR in-box — no NuGet package needed for the server side.
  Client side needs `@microsoft/signalr` npm package.
- `app.MapHub` must come after `app.UseCors(...)` in the middleware pipeline.
