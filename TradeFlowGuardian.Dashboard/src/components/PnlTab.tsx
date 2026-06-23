import { useCallback, useMemo, useState } from 'react'
import { api, type PnlRange, type TradeRecord } from '../api/client'
import { usePolling } from '../hooks/usePolling'
import { PnlChart, type ChartBar } from './PnlChart'
import { TradeList } from './TradeList'

function computePnlQuote(t: TradeRecord): number | null {
  if (t.exitPrice === null) return null
  const raw = t.direction === 'Long'
    ? (t.exitPrice - t.entryPrice) * t.units
    : (t.entryPrice - t.exitPrice) * Math.abs(t.units)
  return Math.round(raw * 100) / 100
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
// matching the window the API buckets by close date.
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

  const pnlFetcher   = useCallback(() => api.getPnl(range), [range])
  const tradeFetcher = useCallback(() => api.getTrades(), [])

  const { data: pnlData, error: pnlError, loading: pnlLoading } = usePolling(pnlFetcher, 60_000)
  const { data: trades }                                        = usePolling(tradeFetcher, 60_000)

  const window = useMemo(() => buildWindow(range), [range])

  // Closed trades realized within the current period (by close date).
  const periodTrades = useMemo(() => {
    if (!trades) return []
    const startMs = window.start.getTime()
    return trades
      .filter(t => t.exitPrice !== null && t.closedAt !== null && new Date(t.closedAt).getTime() >= startMs)
      .map(t => ({ ...t, pnlQuote: computePnlQuote(t) }))
  }, [trades, window])

  // Chart bars: merge the API's daily buckets onto the full period frame, zero-filling gaps.
  const bars = useMemo<ChartBar[]>(() => {
    const byDate = new Map((pnlData ?? []).map(d => [d.date, d]))
    return window.days.map(day => {
      const rec = byDate.get(day.date)
      return {
        date: day.date,
        pnl: rec?.pnl ?? 0,
        tradeCount: rec?.tradeCount ?? 0,
        future: day.future,
        isToday: day.isToday,
        label: day.label,
      }
    })
  }, [pnlData, window])

  // Headline figures — total/best come from the chart buckets so they reconcile with the bars.
  const total     = useMemo(() => (pnlData ?? []).reduce((s, d) => s + d.pnl, 0), [pnlData])
  const tradeCount = useMemo(() => (pnlData ?? []).reduce((s, d) => s + d.tradeCount, 0), [pnlData])
  const bestDay   = useMemo(() => {
    const traded = (pnlData ?? []).filter(d => d.tradeCount > 0)
    if (traded.length === 0) return null
    return traded.reduce((best, d) => (d.pnl > best.pnl ? d : best))
  }, [pnlData])
  const winRate = useMemo(() => {
    const closed = periodTrades.filter(t => t.pnlQuote !== null)
    if (closed.length === 0) return null
    return Math.round((closed.filter(t => (t.pnlQuote ?? 0) > 0).length / closed.length) * 100)
  }, [periodTrades])

  // Per-pair breakdown, scoped to the period.
  const breakdown = useMemo(() => {
    const map: Record<string, { count: number; pnl: number; wins: number }> = {}
    periodTrades.forEach(t => {
      if (t.pnlQuote === null) return
      if (!map[t.instrument]) map[t.instrument] = { count: 0, pnl: 0, wins: 0 }
      map[t.instrument].count++
      map[t.instrument].pnl += t.pnlQuote
      if (t.pnlQuote > 0) map[t.instrument].wins++
    })
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
            {pnlData ? `${total >= 0 ? '+' : ''}${total.toFixed(2)}` : '—'}
          </span>
          <span className="text-xs text-gray-500">realized P&amp;L (quote)</span>
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

        {pnlLoading && !pnlData && <p className="text-sm text-gray-500 animate-pulse">Loading…</p>}
        {pnlError                && <p className="text-sm text-red-400">{pnlError}</p>}
        {pnlData                 && <PnlChart bars={bars} />}

        {pnlData && tradeCount === 0 && (
          <p className="text-xs text-gray-600 italic mt-3">No trades closed {range === 'week' ? 'this week' : 'this month'} yet.</p>
        )}
      </div>

      {/* Per-pair breakdown */}
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

      {/* Trade list */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-3">
          Trades closed {range === 'week' ? 'this week' : 'this month'}
        </p>
        <p className="text-xs text-gray-600 mb-3">P&amp;L shown in quote currency (approximate)</p>
        <TradeList trades={periodTrades} />
      </div>
    </div>
  )
}
