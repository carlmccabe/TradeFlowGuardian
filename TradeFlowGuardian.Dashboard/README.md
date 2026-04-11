# TradeFlowGuardian.Dashboard

React 18 PWA providing a real-time control panel for the TradeFlow Guardian execution engine.

Built with Vite 6, TypeScript, Tailwind CSS v4.

---

## Features

| Widget | Polls | Description |
|---|---|---|
| **Balance** | every 10s | OANDA account balance from `GET /api/status/balance` |
| **Open Positions** | every 5s | Per-instrument units, unrealised P&L, average price |
| **Kill Switch** | on click | `POST /api/status/close/{instrument}` per position |
| **Global Pause** | on click | `POST /api/status/pause` — blocks all new entries |
| **Filter Status** | every 10s | Paused flag + daily drawdown state with percentage |

Mobile-first layout (`max-w-2xl`, single column). Dark theme.

---

## Development

```bash
cd TradeFlowGuardian.Dashboard
npm install
npm run dev
```

Dashboard opens at `http://localhost:5173`. API calls are proxied to `http://localhost:5205` (or `http://localhost:8080` when running via Docker).

The Api must be running for data to load. Start it with:

```bash
# From repo root
./scripts/dev.sh
```

---

## Build

```bash
npm run build    # output → dist/
npm run preview  # serve the built dist locally
```

---

## API Integration

All requests go through `src/api/client.ts` — a typed fetch wrapper over `/api/status/*`.

The Vite dev proxy (`vite.config.ts`) forwards `/api/*` to the running Api service so there are no CORS issues during development. In production the dashboard is served from its own origin and relies on the `Dashboard:Origin` CORS policy configured in the Api.

---

## Roadmap

- [ ] P&L chart (daily, weekly) from PostgreSQL `trade_history` table
- [ ] SignalR subscription for real-time push (replaces polling)
