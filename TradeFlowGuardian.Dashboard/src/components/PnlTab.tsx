import { useCallback, useMemo, useState } from 'react'
import { api, type PnlRange, type OandaTradeRecord } from '../api/client'
import { usePolling } from '../hooks/usePolling'
import { PnlChart, type ChartBar } from './PnlChart'

function formatDuration(openedAt: string, closedAt: string): string {
  const secs = Math.round((new Date(closedAt).getTime() - new Date(openedAt).getTime()) / 1000)
  if (secs < 60)   return `${secs}s`
  if (secs < 3600) return `${Math.round(secs / 60)}m`
  if (secs < 86400) return `${(secs / 3600).toFixed(1)}h`
  return `${Math.round(secs / 86400)}d`
}

function isoDay(d: Date): string {
  return d.toISOString().slice(0, 10)
}

interface PeriodWindow {
  start: Date                 // first instant of the period (UTC, inclusive)
  title: string               // "This week" / "This month"
  subtitle: string            // human range, e.g. "22 – 28 Jun" or "June 2026"
  days: { date: string; future: boolean; isToday: boolean; label: string }[]
}

// Builds the day frame for the current week (Mon–Sun) or month (1st–end), in UTC,
// matching the window trades are bucketed into by close date.
function buildWindow(range: PnlRange): PeriodWindow {
  const now = new Date()
  const todayUtc = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()))
  const todayIso = isoDay(todayUtc)

  let start: Date
  let count: number
  if (range === 'month') {
    start = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1))
    count = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 0)).getUTCDate()
  } else {
    const sinceMonday = (todayUtc.getUTCDay() + 6) % 7   // getUTCDay: Sun=0 … Sat=6
    start = new Date(todayUtc)
    start.setUTCDate(todayUtc.getUTCDate() - sinceMonday)
    count = 7
  }

  const days = Array.from({ length: count }, (_, i) => {
    const d = new Date(start)
    d.setUTCDate(start.getUTCDate() + i)
    const date = isoDay(d)
    let label = ''
    if (range === 'week') {
      label = d.toLocaleDateString('en-AU', { weekday: 'narrow', timeZone: 'UTC' })
    } else if (d.getUTCDay() === 1 || d.getUTCDate() === 1) {
      // Month view: only tick week starts (Mondays) and the 1st to avoid clutter.
      label = String(d.getUTCDate())
    }
    return { date, future: date > todayIso, isToday: date === todayIso, label }
  })

  let subtitle: string
  if (range === 'month') {
    subtitle = start.toLocaleDateString('en-AU', { month: 'long', year: 'numeric', timeZone: 'UTC' })
  } else {
    const end = new Date(start)
    end.setUTCDate(start.getUTCDate() + 6)
    const opts: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', timeZone: 'UTC' }
    subtitle = `${start.toLocaleDateString('en-AU', opts)} – ${end.toLocaleDateString('en-AU', opts)}`
  }

  return { start, title: range === 'week' ? 'This week' : 'This month', subtitle, days }
}

export function PnlTab() {
  const [range, setRange] = useState<PnlRange>('week')

  // 45 days always covers the current calendar month; trades are scoped client-side below.
  const fetcher = useCallback(() => api.getOandaTrades(45), [])
  const { data: trades, error, loading } = usePolling(fetcher, 60_000)

  const window = useMemo(() => buildWindow(range), [range])

  // OANDA closed trades realized within the current period (by close date). All P&L in AUD.
  const periodTrades = useMemo<OandaTradeRecord[]>(() => {
    if (!trades) return []
    const startMs = window.start.getTime()
    return trades.filter(t => new Date(t.closedAt).getTime() >= startMs)
  }, [trades, window])

  // Chart bars: bucket period trades by UTC close day onto the full period frame, zero-filling gaps.
  const bars = useMemo<ChartBar[]>(() => {
    const byDate = new Map<string, { pnl: number; count: number }>()
    for (const t of periodTrades) {
      const key = isoDay(new Date(t.closedAt))
      const prev = byDate.get(key) ?? { pnl: 0, count: 0 }
      byDate.set(key, { pnl: prev.pnl + t.realizedPL, count: prev.count + 1 })
    }
    return window.days.map(day => {
      const rec = byDate.get(day.date)
      return {
        date: day.date,
        pnl: Math.round((rec?.pnl ?? 0) * 100) / 100,
        tradeCount: rec?.count ?? 0,
        future: day.future,
        isToday: day.isToday,
        label: day.label,
      }
    })
  }, [periodTrades, window])

  // Headline figures — all derived from the same period trades so they reconcile with the bars.
  const total      = useMemo(() => periodTrades.reduce((s, t) => s + t.realizedPL, 0), [periodTrades])
  const tradeCount = periodTrades.length
  const bestDay    = useMemo(() => {
    const traded = bars.filter(b => b.tradeCount > 0)
    if (traded.length === 0) return null
    return traded.reduce((best, b) => (b.pnl > best.pnl ? b : best))
  }, [bars])
  const winRate = useMemo(() => {
    if (periodTrades.length === 0) return null
    return Math.round((periodTrades.filter(t => t.realizedPL > 0).length / periodTrades.length) * 100)
  }, [periodTrades])

  // Per-pair breakdown, scoped to the period.
  const breakdown = useMemo(() => {
    const map: Record<string, { count: number; pnl: number; wins: number }> = {}
    for (const t of periodTrades) {
      const key = t.instrument ?? 'OTHER'
      if (!map[key]) map[key] = { count: 0, pnl: 0, wins: 0 }
      map[key].count++
      map[key].pnl += t.realizedPL
      if (t.realizedPL > 0) map[key].wins++
    }
    return Object.entries(map).map(([instrument, v]) => ({
      instrument,
      tradeCount: v.count,
      totalPnl: Math.round(v.pnl * 100) / 100,
      winRate: v.count > 0 ? Math.round((v.wins / v.count) * 100) : 0,
    }))
  }, [periodTrades])

  return (
    <div className="flex flex-col gap-5">

      {/* Headline summary + chart */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <div className="flex items-start justify-between mb-4">
          <div>
            <p className="text-xs font-medium uppercase tracking-widest text-gray-500">{window.title}</p>
            <p className="text-[11px] text-gray-600 mt-0.5">{window.subtitle}</p>
          </div>
          <div className="flex bg-gray-800 rounded-lg p-1 gap-1">
            {(['week', 'month'] as PnlRange[]).map(r => (
              <button
                key={r}
                onClick={() => setRange(r)}
                className={`px-3 py-1 rounded-md text-xs font-semibold transition-colors ${
                  range === r ? 'bg-gray-700 text-emerald-400' : 'text-gray-500 hover:text-gray-300'
                }`}
              >
                {r === 'week' ? 'Week' : 'Month'}
              </button>
            ))}
          </div>
        </div>

        {/* Big total */}
        <div className="flex items-baseline gap-2">
          <span className={`text-3xl font-mono font-bold ${total >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
            {trades ? `${total >= 0 ? '+' : ''}${total.toFixed(2)}` : '—'}
          </span>
          <span className="text-xs text-gray-500">realized P&amp;L (AUD)</span>
        </div>

        {/* Sub-stats */}
        <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-gray-400 mt-2 mb-4">
          <span>{tradeCount} trade{tradeCount !== 1 ? 's' : ''} closed</span>
          {winRate !== null && <span>{winRate}% win</span>}
          {bestDay && (
            <span>
              best day <span className={bestDay.pnl >= 0 ? 'text-emerald-400' : 'text-red-400'}>
                {bestDay.pnl >= 0 ? '+' : ''}{bestDay.pnl.toFixed(2)}
              </span>
            </span>
          )}
        </div>

        {loading && !trades && <p className="text-sm text-gray-500 animate-pulse">Loading…</p>}
        {error              && <p className="text-sm text-red-400">{error}</p>}
        {trades             && <PnlChart bars={bars} />}

        {trades && tradeCount === 0 && (
          <p className="text-xs text-gray-600 italic mt-3">No trades closed {range === 'week' ? 'this week' : 'this month'} yet.</p>
        )}
      </div>

      {/* Per-instrument breakdown */}
      {breakdown.length > 0 && (
        <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
          <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-3">
            By instrument
          </p>
          <div className="flex flex-col gap-2">
            {breakdown.map(b => (
              <div key={b.instrument} className="flex items-center justify-between text-sm">
                <span className="font-mono text-gray-200">{b.instrument.replace('_', '/')}</span>
                <div className="flex items-center gap-4">
                  <span className="text-gray-400">{b.tradeCount} trade{b.tradeCount !== 1 ? 's' : ''}</span>
                  <span className="text-gray-400">{b.winRate}% win</span>
                  <span className={`font-mono font-semibold ${b.totalPnl >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                    {b.totalPnl >= 0 ? '+' : ''}{b.totalPnl.toFixed(2)}
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Closed trade list */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-1">
          Trades closed {range === 'week' ? 'this week' : 'this month'}
        </p>
        <p className="text-xs text-gray-600 mb-3">P&amp;L in AUD from OANDA</p>
        {trades && periodTrades.length === 0 && (
          <p className="text-sm text-gray-500 italic">No closed trades in the selected period.</p>
        )}
        <div className="flex flex-col gap-2">
          {periodTrades.map(t => {
            const isLong = t.units > 0
            const pnlColor = t.realizedPL >= 0 ? 'text-emerald-400' : 'text-red-400'
            return (
              <div key={t.id} className="rounded-lg bg-gray-800 px-4 py-3 flex items-center justify-between gap-3 text-sm">
                <div className="flex items-center gap-2 min-w-0">
                  <span className={`text-xs font-bold px-2 py-0.5 rounded flex-shrink-0 ${
                    isLong ? 'bg-emerald-900 text-emerald-300' : 'bg-red-900 text-red-300'
                  }`}>
                    {isLong ? 'LONG' : 'SHORT'}
                  </span>
                  <span className="font-mono text-gray-100 truncate">
                    {(t.instrument ?? 'UNKNOWN').replace('_', '/')}
                  </span>
                </div>
                <div className="flex items-center gap-4 text-right flex-shrink-0">
                  <div className="hidden sm:block">
                    <p className="text-xs text-gray-500">Entry → Close</p>
                    <p className="font-mono text-gray-300 text-xs">
                      {t.entryPrice.toFixed(t.entryPrice > 10 ? 3 : 5)}
                      {t.closePrice > 0 && ` → ${t.closePrice.toFixed(t.closePrice > 10 ? 3 : 5)}`}
                    </p>
                  </div>
                  <div className="hidden sm:block">
                    <p className="text-xs text-gray-500">Duration</p>
                    <p className="font-mono text-gray-300 text-xs">
                      {formatDuration(t.openedAt, t.closedAt)}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-gray-500">P&amp;L (AUD)</p>
                    <p className={`font-mono font-semibold ${pnlColor}`}>
                      {t.realizedPL >= 0 ? '+' : ''}{t.realizedPL.toFixed(2)}
                    </p>
                  </div>
                </div>
              </div>
            )
          })}
        </div>
      </div>
    </div>
  )
}
