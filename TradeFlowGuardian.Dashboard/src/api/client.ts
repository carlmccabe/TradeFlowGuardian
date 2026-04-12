// In dev, VITE_API_URL is unset and Vite's proxy rewrites /api → localhost:5205.
// In production, set VITE_API_URL=https://your-api.railway.app (no trailing slash).
const BASE = import.meta.env.VITE_API_URL
  ? `${import.meta.env.VITE_API_URL}/api`
  : '/api'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    // no-store bypasses the HTTP cache entirely — prevents 304s on polling calls
    cache: 'no-store',
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  })
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  return res.json() as Promise<T>
}

// ── Status endpoints ──────────────────────────────────────────────────────────

export interface BalanceResponse {
  balanceAud: number
  fetchedAt: string
}

export interface PositionResponse {
  instrument: string
  units: number
  unrealizedPL: number
  averagePrice: number
}

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

export const api = {
  getBalance: () => request<BalanceResponse>('/status/balance'),

  getPositions: () => request<PositionResponse[]>('/status/positions'),

  getFilterStatus: () => request<FilterStatusResponse>('/status/filters'),

  closePosition: (instrument: string) =>
    request<void>(`/status/close/${instrument}`, { method: 'POST' }),

  setPaused: (paused: boolean) =>
    request<void>('/status/pause', {
      method: 'POST',
      body: JSON.stringify({ paused }),
    }),
}
