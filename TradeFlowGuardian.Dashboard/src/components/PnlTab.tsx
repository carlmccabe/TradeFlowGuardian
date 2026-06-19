import { useCallback, useMemo, useState } from 'react'
import { api, type TradeRecord } from '../api/client'
import { usePolling } from '../hooks/usePolling'
import { PnlChart } from './PnlChart'
import { TradeList } from './TradeList'

type Range = 'daily' | 'weekly'

function computePnlQuote(t: TradeRecord): number | null {
  if (t.exitPrice === null) return null
  const raw = t.direction === 'Long'
    ? (t.exitPrice - t.entryPrice) * t.units
    : (t.entryPrice - t.exitPrice) * Math.abs(t.units)
  return Math.round(raw * 100) / 100
}

export function PnlTab() {
  const [range, setRange] = useState<Range>('daily')

  const pnlFetcher  = useCallback(() => api.getPnl(range), [range])
  const tradeFetcher = useCallback(() => api.getTrades(), [])

  const { data: pnlData, error: pnlError, loading: pnlLoading } = usePolling(pnlFetcher, 60_000)
  const { data: trades }                                         = usePolling(tradeFetcher, 60_000)

  const enriched = useMemo(() => {
    if (!trades) return []
    return trades.map(t => ({ ...t, pnlQuote: computePnlQuote(t) }))
  }, [trades])

  // Per-pair breakdown across all closed trades
  const breakdown = useMemo(() => {
    const map: Record<string, { count: number; pnl: number; wins: number }> = {}
    enriched.forEach(t => {
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
  }, [enriched])

  const totalPnl = useMemo(
    () => pnlData?.reduce((s, d) => s + d.pnl, 0) ?? 0,
    [pnlData]
  )

  return (
    <div className="flex flex-col gap-5">

      {/* Chart + toggle */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <div className="flex items-center justify-between mb-4">
          <div>
            <p className="text-xs font-medium uppercase tracking-widest text-gray-500">
              Realized P&amp;L
            </p>
            {pnlData && (
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

        {pnlLoading && <p className="text-sm text-gray-500 animate-pulse">Loading…</p>}
        {pnlError   && <p className="text-sm text-red-400">{pnlError}</p>}
        {pnlData    && <PnlChart bars={pnlData} weekly={range === 'weekly'} />}

        {pnlData && range === 'weekly' && pnlData.length > 0 && (
          <div className="flex justify-between mt-2">
            {pnlData.map(bar => {
              const d = new Date(bar.date + 'T00:00:00Z')
              const label = d.toLocaleDateString('en-AU', { month: 'short', day: 'numeric', timeZone: 'UTC' })
              return (
                <span key={bar.date} className="text-[10px] text-gray-600 flex-1 text-center">
                  {label}
                </span>
              )
            })}
          </div>
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
          Closed trades
        </p>
        <p className="text-xs text-gray-600 mb-3">P&amp;L shown in quote currency (approximate)</p>
        <TradeList trades={enriched.filter(t => t.exitPrice !== null)} />
      </div>
    </div>
  )
}
