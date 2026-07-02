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

// Like request(), but surfaces the server's { error } message on failure.
// Used where the API returns actionable validation errors (e.g. backtest runs).
async function requestVerbose<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    cache: 'no-store',
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  })
  if (!res.ok) {
    let message = `${res.status} ${res.statusText}`
    try {
      const body = await res.json()
      if (body?.error) message = body.error
    } catch { /* non-JSON error body */ }
    throw new Error(message)
  }
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

export interface DailyPnlRecord {
  date: string        // "YYYY-MM-DD" (daily = that day, weekly = Monday of the week)
  pnl: number
  tradeCount: number
}

// ── Backtest ──────────────────────────────────────────────────────────────────

export interface RunBacktestRequest {
  name: string
  strategyPreset: string
  instrument: string
  timeframe: string
  startDate: string          // ISO
  endDate: string            // ISO
  initialBalance: number
  riskPerTrade: number       // fraction, e.g. 0.025
  fastPeriods?: number       // emac_custom only
  slowPeriods?: number       // emac_custom only
  slMultiplier?: number      // tfg presets only
  tpMultiplier?: number      // tfg presets only
  leverage?: number
  marginUtilisationLimit?: number
  maxPositionUnits?: number
}

export interface BacktestMetrics {
  totalTrades: number
  winningTrades: number
  losingTrades: number
  winRate: number
  profitFactor: number
  averageWin: number
  averageLoss: number
  largestWin: number
  largestLoss: number
  maxDrawdown: number
  sharpeRatio: number
  sortinoRatio: number
  calmarRatio: number
  expectancyRatio: number
  monthlyBreakdown: MonthlyPerformance[]
}

export interface MonthlyPerformance {
  year: number
  month: number
  pnL: number
  trades: number
  wins: number
  winRate: number
  averageR: number
  label: string
}

export interface BacktestEquityPoint {
  timestamp: string
  balance: number
  equity: number
  drawdownPercent: number
}

export interface BacktestTradeRecord {
  tradeNumber: number
  instrument: string
  direction: 'Long' | 'Short'
  entryTime: string
  entryPrice: number
  exitTime: string
  exitPrice: number
  units: number
  stopLoss: number | null
  takeProfit: number | null
  pnL: number
  exitReason: string
  rMultiple: number | null
}

export interface BacktestResultResponse {
  id: string
  name: string
  strategyName: string
  instrument: string
  timeframe: string
  startDate: string
  endDate: string
  initialBalance: number
  finalBalance: number
  totalReturn: number
  trades: BacktestTradeRecord[]
  equityCurve: BacktestEquityPoint[]
  metrics: BacktestMetrics
}

export interface BacktestRunSummary {
  id: string
  name: string
  strategyName: string
  instrument: string
  timeframe: string
  startDate: string
  endDate: string
  initialBalance: number
  finalBalance: number
  totalReturn: number
  maxDrawdown: number
  sharpeRatio: number | null
  winRate: number | null
  totalTrades: number
  createdAt: string
}

export interface DataCoverageResponse {
  instrument: string
  timeframe: string
  candlesFound: number
  candlesExpected: number
  coveragePercent: number
  isAvailable: boolean
  earliestCached: string | null
  latestCached: string | null
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

  getTrades: () => request<TradeRecord[]>('/status/trades'),

  getPnl: (range: 'daily' | 'weekly') =>
    request<DailyPnlRecord[]>(`/status/pnl?range=${range}`),

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

  runBacktest: (body: RunBacktestRequest) =>
    requestVerbose<BacktestResultResponse>('/backtest/run', {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  getBacktestRuns: (limit = 20) =>
    request<BacktestRunSummary[]>(`/backtest/runs?limit=${limit}`),

  getBacktestRun: (id: string) =>
    request<BacktestResultResponse>(`/backtest/runs/${id}`),

  getBacktestStrategies: () => request<string[]>('/backtest/strategies'),

  getDataCoverage: (instrument: string, timeframe: string, startDate: string, endDate: string) =>
    request<DataCoverageResponse>(
      `/backtest/data/coverage?instrument=${instrument}&timeframe=${timeframe}&startDate=${startDate}&endDate=${endDate}`),

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
