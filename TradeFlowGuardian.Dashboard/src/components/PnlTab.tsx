import { useCallback, useMemo, useState } from 'react'
import { api, type OandaTradeRecord, type DailyPnlRecord } from '../api/client'
import { usePolling } from '../hooks/usePolling'
import { PnlChart } from './PnlChart'

type Range = 'daily' | 'weekly'

// Groups OANDA closed trades by UTC day or ISO week (Monday), returning chart bars.
function groupByDate(trades: OandaTradeRecord[], weekly: boolean): DailyPnlRecord[] {
  const map = new Map<string, { pnl: number; count: number }>()
  for (const t of trades) {
    const d = new Date(t.closedAt)
    let key: string
    if (weekly) {
      const monday = new Date(d)
      const dow = monday.getUTCDay()                      // 0=Sun … 6=Sat
      monday.setUTCDate(monday.getUTCDate() - (dow === 0 ? 6 : dow - 1))
      monday.setUTCHours(0, 0, 0, 0)
      key = monday.toISOString().slice(0, 10)
    } else {
      key = d.toISOString().slice(0, 10)
    }
    const prev = map.get(key) ?? { pnl: 0, count: 0 }
    map.set(key, { pnl: prev.pnl + t.realizedPL, count: prev.count + 1 })
  }
  return Array.from(map.entries())
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([date, { pnl, count }]) => ({
      date,
      pnl: Math.round(pnl * 100) / 100,
      tradeCount: count,
    }))
}

function formatLabel(date: string, weekly: boolean): string {
  const d = new Date(date + 'T00:00:00Z')
  return d.toLocaleDateString('en-AU', {
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
    ...(weekly ? {} : { weekday: 'short' }),
  })
}

export function PnlTab() {
  const [range, setRange] = useState<Range>('daily')

  const fetcher = useCallback(
    () => api.getOandaTrades(range === 'weekly' ? 90 : 30),
    [range],
  )
  const { data: trades, error, loading } = usePolling(fetcher, 60_000)

  const bars = useMemo(
    () => (trades ? groupByDate(trades, range === 'weekly') : []),
    [trades, range],
  )

  const totalPnl = useMemo(
    () => trades?.reduce((s, t) => s + t.realizedPL, 0) ?? 0,
    [trades],
  )

  // Per-instrument breakdown
  const breakdown = useMemo(() => {
    const map: Record<string, { count: number; pnl: number; wins: number }> = {}
    for (const t of trades ?? []) {
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
  }, [trades])

  return (
    <div className="flex flex-col gap-5">

      {/* Chart card */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <div className="flex items-center justify-between mb-4">
          <div>
            <p className="text-xs font-medium uppercase tracking-widest text-gray-500">
              Realized P&amp;L (AUD)
            </p>
            {trades && (
              <p className={`text-lg font-mono font-semibold mt-0.5 ${totalPnl >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                {totalPnl >= 0 ? '+' : ''}{totalPnl.toFixed(2)}
              </p>
            )}
          </div>
          <div className="flex bg-gray-800 rounded-lg p-1 gap-1">
            {(['daily', 'weekly'] as Range[]).map(r => (
              <button
                key={r}
                onClick={() => setRange(r)}
                className={`px-3 py-1 rounded-md text-xs font-semibold transition-colors ${
                  range === r
                    ? 'bg-gray-700 text-emerald-400'
                    : 'text-gray-500 hover:text-gray-300'
                }`}
              >
                {r.charAt(0).toUpperCase() + r.slice(1)}
              </button>
            ))}
          </div>
        </div>

        {loading && <p className="text-sm text-gray-500 animate-pulse">Loading…</p>}
        {error   && <p className="text-sm text-red-400">{error}</p>}
        {!loading && !error && <PnlChart bars={bars} weekly={range === 'weekly'} />}

        {/* X-axis labels for weekly */}
        {!loading && !error && range === 'weekly' && bars.length > 0 && (
          <div className="flex mt-2">
            {bars.map(bar => (
              <span key={bar.date} className="text-[10px] text-gray-600 flex-1 text-center">
                {formatLabel(bar.date, true)}
              </span>
            ))}
          </div>
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
          Closed trades
        </p>
        <p className="text-xs text-gray-600 mb-3">P&amp;L in AUD from OANDA</p>
        {!loading && trades?.length === 0 && (
          <p className="text-sm text-gray-500 italic">No closed trades in the selected period.</p>
        )}
        <div className="flex flex-col gap-2">
          {(trades ?? []).map(t => {
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
                    <p className="text-xs text-gray-500">Entry</p>
                    <p className="font-mono text-gray-300">
                      {t.entryPrice.toFixed(t.entryPrice > 10 ? 3 : 5)}
                    </p>
                  </div>
                  <div className="hidden sm:block">
                    <p className="text-xs text-gray-500">Closed</p>
                    <p className="font-mono text-gray-300 text-xs">
                      {new Date(t.closedAt).toLocaleDateString('en-AU', { month: 'short', day: 'numeric' })}
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
