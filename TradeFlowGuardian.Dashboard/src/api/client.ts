// In dev, VITE_API_URL is unset and Vite's proxy rewrites /api → localhost:5205.
// In production, set VITE_API_URL=https://your-api.railway.app (no trailing slash).
const BASE = `${import.meta.env.VITE_API_URL ?? ''}/api`

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  })
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  return res.json() as Promise<T>
}

// ── Known tradeable instruments ───────────────────────────────────────────────

export const INSTRUMENTS = ['USD_JPY', 'EUR_USD', 'GBP_USD'] as const
export type Instrument = (typeof INSTRUMENTS)[number]

// ── Status endpoints ──────────────────────────────────────────────────────────

export interface BalanceResponse {
  balanceAud: number
  fetchedAt: string
}

// Matches GET /api/status/position/{instrument}
export interface PositionResponse {
  instrument: string
  units: number
  side: 'LONG' | 'SHORT' | 'FLAT'
  fetchedAt: string
}

// Matches GET /api/status/filters
export interface FilterStatusResponse {
  paused: boolean
  dailyDrawdown: {
    isBreached: boolean
    dayOpenNav: number | null
    currentBalance: number | null
    drawdownPercent: number | null
    maxDrawdownPercent: number
    tradingDay: string
  }
  fetchedAt: string
}

// ── Price endpoints ───────────────────────────────────────────────────────────

// Matches GET /api/price/price/{instrument}
export interface PriceResponse {
  instrument: string
  mid: number
  fetchedAt: string
}

// ── Health endpoint ───────────────────────────────────────────────────────────

// Matches GET /api/signal/health
export interface HealthResponse {
  status: string
  utc: string
}

export const api = {
  getBalance: () => request<BalanceResponse>('/status/balance'),

  // Fetches position for a single instrument
  getPosition: (instrument: string) =>
    request<PositionResponse>(`/status/position/${instrument}`),

  // Fetches all known instruments in parallel, returns only open (non-FLAT) positions
  getPositions: () =>
    Promise.all(INSTRUMENTS.map((i) => request<PositionResponse>(`/status/position/${i}`))).then(
      (results) => results.filter((p) => p.side !== 'FLAT'),
    ),

  getFilterStatus: () => request<FilterStatusResponse>('/status/filters'),

  setPaused: (paused: boolean) =>
    request<void>('/status/pause', {
      method: 'POST',
      body: JSON.stringify({ paused }),
    }),

  // Live mid price for a single instrument
  getPrice: (instrument: string) =>
    request<PriceResponse>(`/price/price/${instrument}`),

  // All prices in parallel
  getPrices: () =>
    Promise.all(INSTRUMENTS.map((i) => request<PriceResponse>(`/price/price/${i}`))),

  // System health
  getHealth: () => request<HealthResponse>('/signal/health'),

  closePosition: (instrument: string) =>
    request<void>(`/status/close/${instrument}`, { method: 'POST' }),
}
