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
  // From the entry order's local history row; null when no matching row exists
  // (manual trades, or trades placed before sizing transparency).
  stopLoss: number | null
  takeProfit: number | null
  projectedLossAud: number | null    // AUD lost if the stop is hit
  projectedProfitAud: number | null  // AUD gained if the target is hit
  riskPercent: number | null
  riskAmount: number | null
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

export interface TradeSizing {
  riskPercent: number
  riskSource: string       // 'signal-override' | 'db' | 'config-default'
  accountBalance: number   // AUD at sizing time
  riskAmount: number       // AUD at risk = balance × risk%
  atr: number
  stopDistance: number
  stopSource: string       // 'signal-sl' | 'atr×N'
  quoteToAud: number
  capReason: string | null // null | 'margin-cap' | 'max-position-units' | 'aborted'
}

export interface TradeRecord {
  instrument: string
  direction: 'Long' | 'Short'
  entryPrice: number
  exitPrice: number | null
  units: number
  openedAt: string
  closedAt: string | null
  durationSeconds: number | null
  stopLoss: number | null
  takeProfit: number | null
  projectedLossAud: number | null
  projectedProfitAud: number | null
  sizing: TradeSizing | null  // null for trades placed before migration 007
}

export type TradesRange = 'week' | 'month' | 'quarter' | 'all'

export interface DailyPnlRecord {
  date: string        // "YYYY-MM-DD" — the UTC day the trade was closed (P&L realized)
  pnl: number
  tradeCount: number
}

export type PnlRange = 'week' | 'month'

export interface OandaTradeRecord {
  id: string
  instrument: string | null
  units: number        // positive = long, negative = short (OANDA initialUnits)
  entryPrice: number
  closePrice: number   // averageClosePrice from OANDA (0 if unavailable)
  realizedPL: number   // in account currency (AUD), net of commission & financing
  openedAt: string     // ISO timestamp — when the position was opened
  closedAt: string     // ISO timestamp — when the position was closed
}

// ── Accounts ──────────────────────────────────────────────────────────────────

export interface AccountResponse {
  id: string
  label: string
  accountId: string
  environment: 'fxpractice' | 'fxtrade'
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ActiveAccountResponse {
  label: string
  accountId: string
  environment: 'fxpractice' | 'fxtrade'
  isLive: boolean
}

export interface CreateAccountRequest {
  label: string
  accountId: string
  environment: 'fxpractice' | 'fxtrade'
  apiKey: string
  activate: boolean
  confirmLive: boolean
}

// Admin secret for account management endpoints (X-Admin-Secret header).
// Kept in localStorage so it survives reloads on the user's own device.
const ADMIN_SECRET_KEY = 'tfg_admin_secret'

export const adminSecret = {
  get: () => localStorage.getItem(ADMIN_SECRET_KEY) ?? '',
  set: (value: string) => localStorage.setItem(ADMIN_SECRET_KEY, value),
  clear: () => localStorage.removeItem(ADMIN_SECRET_KEY),
}

// Like request(), but sends the admin secret and surfaces the server's error message.
async function adminRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    cache: 'no-store',
    ...init,
    headers: {
      'Content-Type': 'application/json',
      'X-Admin-Secret': adminSecret.get(),
      ...init?.headers,
    },
  })
  if (!res.ok) {
    let message = `${res.status} ${res.statusText}`
    try {
      const body = await res.json()
      if (body?.error) message = body.error
    } catch { /* non-JSON error body */ }
    throw new Error(message)
  }
  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

// ── API client ────────────────────────────────────────────────────────────────

export const api = {
  getBalance: () => request<BalanceResponse>('/status/balance'),

  getStatus: () => request<StatusResponse>('/status'),

  getPositions: () => request<PositionResponse[]>('/status/positions'),

  getFilterStatus: () => request<FilterStatusResponse>('/status/filters'),

  getTrades: (opts?: { range?: TradesRange; from?: string; to?: string }) => {
    const q = new URLSearchParams()
    if (opts?.from || opts?.to) {
      if (opts.from) q.set('from', opts.from)
      if (opts.to) q.set('to', opts.to)
    } else if (opts?.range) {
      q.set('range', opts.range)
    }
    const qs = q.toString()
    return request<TradeRecord[]>(`/status/trades${qs ? `?${qs}` : ''}`)
  },

  getPnl: (range: PnlRange) =>
    request<DailyPnlRecord[]>(`/status/pnl?range=${range}`),

  getOandaTrades: (days = 30) =>
    request<OandaTradeRecord[]>(`/status/history?days=${days}`),

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

  getActiveAccount: () => request<ActiveAccountResponse>('/accounts/active'),

  getAccounts: () => adminRequest<AccountResponse[]>('/accounts'),

  createAccount: (body: CreateAccountRequest) =>
    adminRequest<AccountResponse>('/accounts', {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  activateAccount: (id: string, confirmLive: boolean) =>
    adminRequest<AccountResponse>(`/accounts/${id}/activate`, {
      method: 'PUT',
      body: JSON.stringify({ confirmLive }),
    }),

  deleteAccount: (id: string) =>
    adminRequest<void>(`/accounts/${id}`, { method: 'DELETE' }),
}
