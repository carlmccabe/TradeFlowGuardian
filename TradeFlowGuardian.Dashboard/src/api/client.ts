// In dev, VITE_API_URL is unset and Vite's proxy rewrites /api → localhost:5205.
// In production, set VITE_API_URL=https://your-api.railway.app (no trailing slash).
const BASE = import.meta.env.VITE_API_URL
  ? `${import.meta.env.VITE_API_URL}/api`
  : '/api'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    cache: 'no-store',
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  })
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  return res.json() as Promise<T>
}

// ── Status ────────────────────────────────────────────────────────────────────

export interface BalanceResponse {
  balanceAud: number
  fetchedAt: string
}

export interface PositionResponse {
  instrument: string
  units: number
  unrealizedPL: number
  averagePrice: number
  side: 'LONG' | 'SHORT' | 'FLAT'
}

export interface StatusResponse {
  balanceAud: number
  positions: PositionResponse[]
  fetchedAt: string
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

// ── Risk ──────────────────────────────────────────────────────────────────────

export interface RiskSettingsResponse {
  instrument: string
  riskPercent: number
  isActive: boolean
  updatedAt: string
}

// ── Trades ────────────────────────────────────────────────────────────────────

export interface TradeRecord {
  instrument: string
  direction: 'Long' | 'Short'
  entryPrice: number
  exitPrice: number | null
  units: number
  openedAt: string
  closedAt: string | null
  durationSeconds: number | null
}

// ── API client ────────────────────────────────────────────────────────────────

export const api = {
  getBalance: () => request<BalanceResponse>('/status/balance'),

  getStatus: () => request<StatusResponse>('/status'),

  getPositions: () => request<PositionResponse[]>('/status/positions'),

  getFilterStatus: () => request<FilterStatusResponse>('/status/filters'),

  getTrades: () => request<TradeRecord[]>('/status/trades'),

  closePosition: (instrument: string) =>
    request<void>(`/status/close/${instrument}`, { method: 'POST' }),

  setPaused: (paused: boolean) =>
    request<void>('/status/pause', {
      method: 'POST',
      body: JSON.stringify({ paused }),
    }),

  getRiskSettings: () => request<RiskSettingsResponse[]>('/risk'),

  updateRisk: (instrument: string, riskPercent?: number, isActive?: boolean) =>
    request<RiskSettingsResponse>(`/risk/${instrument}`, {
      method: 'PATCH',
      body: JSON.stringify({ riskPercent, isActive }),
    }),

  pauseAll: () => request<void>('/risk/pause-all', { method: 'POST' }),

  resumeAll: () => request<void>('/risk/resume-all', { method: 'POST' }),
}
