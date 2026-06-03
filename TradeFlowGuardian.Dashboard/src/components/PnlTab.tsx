import { useCallback, useMemo, useState } from 'react'
import { api, type TradeRecord } from '../api/client'
import { usePolling } from '../hooks/usePolling'
import { PnlChart, buildWeekGroups } from './PnlChart'
import { TradeList } from './TradeList'

function computePnlQuote(t: TradeRecord): number | null {
  if (t.exitPrice === null) return null
  const raw = t.direction === 'Long'
    ? (t.exitPrice - t.entryPrice) * t.units
    : (t.entryPrice - t.exitPrice) * Math.abs(t.units)
  return Math.round(raw * 100) / 100
}

export function PnlTab() {
  const fetcher = useCallback(() => api.getTrades(), [])
  const { data, error, loading } = usePolling(fetcher, 60_000)
  const [selectedWeek, setSelectedWeek] = useState(0)

  const enriched = useMemo(() => {
    if (!data) return []
    return data.map(t => ({ ...t, pnlQuote: computePnlQuote(t) }))
  }, [data])

  const weeks = useMemo(() =>
    buildWeekGroups(enriched.map(t => ({
      openedAt: t.openedAt,
      pnlQuote: t.pnlQuote ?? 0,
    }))),
    [enriched]
  )

  // Filter trades for selected week
  const weekTrades = useMemo(() => {
    if (weeks.length === 0) return []
    const dates = new Set(weeks[selectedWeek].days.map(d => d.date))
    return enriched.filter(t => dates.has(t.openedAt.slice(0, 10)))
  }, [enriched, weeks, selectedWeek])

  // Per-pair breakdown for selected week
  const breakdown = useMemo(() => {
    const map: Record<string, { count: number; pnl: number; wins: number }> = {}
    weekTrades.forEach(t => {
      if (!map[t.instrument]) map[t.instrument] = { count: 0, pnl: 0, wins: 0 }
      map[t.instrument].count++
      const pnl = t.pnlQuote ?? 0
      map[t.instrument].pnl += pnl
      if (pnl > 0) map[t.instrument].wins++
    })
    return Object.entries(map).map(([instrument, v]) => ({
      instrument,
      tradeCount: v.count,
      totalPnl: Math.round(v.pnl * 100) / 100,
      winRate: v.count > 0 ? Math.round((v.wins / v.count) * 100) : 0,
    }))
  }, [weekTrades])

  if (loading) return <p className="text-sm text-gray-500 animate-pulse">Loading trades…</p>
  if (error)   return <p className="text-sm text-red-400">{error}</p>

  return (
    <div className="flex flex-col gap-5">
      {/* Weekly P&L chart — selectable */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-4">
          Weekly P&amp;L
        </p>
        <PnlChart weeks={weeks} selectedWeek={selectedWeek} onSelectWeek={setSelectedWeek} />
      </div>

      {/* Per-pair breakdown */}
      {breakdown.length > 0 && (
        <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
          <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-3">
            {weeks[selectedWeek]?.weekLabel ?? 'This week'} — breakdown
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
          Trades — {weeks[selectedWeek]?.weekLabel ?? 'This week'}
        </p>
        <p className="text-xs text-gray-600 mb-3">P&amp;L shown in quote currency (approximate)</p>
        <TradeList trades={weekTrades} />
      </div>
    </div>
  )
}
