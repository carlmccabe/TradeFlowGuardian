import { useCallback, useState } from 'react'
import { api, type TradeRecord, type TradesRange } from '../api/client'
import { usePolling } from '../hooks/usePolling'

const RANGES: { key: TradesRange; label: string }[] = [
  { key: 'week', label: 'Last week' },
  { key: 'month', label: 'Last month' },
  { key: 'quarter', label: 'Last quarter' },
  { key: 'all', label: 'All time' },
]

function formatDuration(secs: number | null): string {
  if (secs === null) return 'open'
  if (secs < 60) return `${secs}s`
  if (secs < 3600) return `${Math.round(secs / 60)}m`
  if (secs < 86400) return `${(secs / 3600).toFixed(1)}h`
  return `${Math.round(secs / 86400)}d`
}

function px(value: number): string {
  return value.toFixed(value > 10 ? 3 : 5)
}

function aud(value: number, signed = false): string {
  const s = value.toLocaleString('en-AU', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  return signed && value >= 0 ? `+$${s}` : value < 0 ? `−$${Math.abs(value).toLocaleString('en-AU', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}` : `$${s}`
}

export function TradesTab() {
  const [range, setRange] = useState<TradesRange>('month')
  const [custom, setCustom] = useState(false)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  // Applied custom window — rows only refetch when Apply is pressed, not per keystroke.
  const [applied, setApplied] = useState<{ from?: string; to?: string } | null>(null)

  const fetcher = useCallback(() => {
    if (custom && applied) return api.getTrades(applied)
    return api.getTrades({ range })
  }, [range, custom, applied])
  const { data: trades, error, loading } = usePolling(fetcher, 60_000)

  return (
    <div className="flex flex-col gap-4">
      {/* Range controls */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-4 flex flex-col gap-3">
        <div className="flex flex-wrap gap-1.5">
          {RANGES.map(r => (
            <button
              key={r.key}
              onClick={() => { setRange(r.key); setCustom(false) }}
              className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors ${
                !custom && range === r.key
                  ? 'bg-gray-700 text-emerald-400'
                  : 'bg-gray-800 text-gray-500 hover:text-gray-300'
              }`}
            >
              {r.label}
            </button>
          ))}
          <button
            onClick={() => setCustom(true)}
            className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors ${
              custom ? 'bg-gray-700 text-emerald-400' : 'bg-gray-800 text-gray-500 hover:text-gray-300'
            }`}
          >
            Set dates
          </button>
        </div>

        {custom && (
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <input
              type="date" value={from} onChange={e => setFrom(e.target.value)}
              className="bg-gray-800 border border-gray-700 rounded-lg px-2 py-1.5 text-gray-200 [color-scheme:dark]"
            />
            <span className="text-gray-500">to</span>
            <input
              type="date" value={to} onChange={e => setTo(e.target.value)}
              className="bg-gray-800 border border-gray-700 rounded-lg px-2 py-1.5 text-gray-200 [color-scheme:dark]"
            />
            <button
              onClick={() => setApplied({ from: from || undefined, to: to || undefined })}
              disabled={!from && !to}
              className="px-3 py-1.5 rounded-lg font-semibold bg-emerald-800 hover:bg-emerald-700 text-emerald-200 disabled:opacity-40 transition-colors"
            >
              Apply
            </button>
          </div>
        )}
      </div>

      {loading && !trades && <p className="text-sm text-gray-500 animate-pulse px-1">Loading trades…</p>}
      {error && <p className="text-sm text-red-400 px-1">{error}</p>}
      {trades && trades.length === 0 && (
        <p className="text-sm text-gray-500 italic px-1">No trades in this window.</p>
      )}

      <div className="flex flex-col gap-2">
        {(trades ?? []).map((t, i) => <TradeCard key={`${t.instrument}-${t.openedAt}-${i}`} trade={t} />)}
      </div>
    </div>
  )
}

function TradeCard({ trade: t }: { trade: TradeRecord }) {
  const [open, setOpen] = useState(false)
  const isLong = t.direction === 'Long'
  const opened = new Date(t.openedAt)
  const rr = t.projectedLossAud && t.projectedProfitAud && t.projectedLossAud > 0
    ? t.projectedProfitAud / t.projectedLossAud
    : null

  return (
    <div className="rounded-xl bg-gray-900 border border-gray-800">
      <button onClick={() => setOpen(o => !o)} className="w-full text-left p-4 flex flex-col gap-2.5">
        {/* Header: pair, side, date, duration */}
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-2 min-w-0">
            <span className={`text-xs font-bold px-2 py-0.5 rounded flex-shrink-0 ${
              isLong ? 'bg-emerald-900 text-emerald-300' : 'bg-red-900 text-red-300'
            }`}>
              {isLong ? 'LONG' : 'SHORT'}
            </span>
            <span className="font-mono font-bold text-gray-100">{t.instrument.replace('_', '/')}</span>
            {t.closedAt === null && (
              <span className="text-xs font-semibold px-2 py-0.5 rounded bg-sky-950 text-sky-400">OPEN</span>
            )}
          </div>
          <span className="text-xs text-gray-500 font-mono flex-shrink-0">
            {opened.toLocaleDateString('en-AU', { day: 'numeric', month: 'short' })} · {formatDuration(t.durationSeconds)}
          </span>
        </div>

        {/* Prices + projections */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-x-3 gap-y-1.5 text-xs">
          <div>
            <p className="text-gray-500 uppercase tracking-wider">Entry{t.exitPrice !== null ? ' → Exit' : ''}</p>
            <p className="font-mono text-gray-200">
              {px(t.entryPrice)}{t.exitPrice !== null && ` → ${px(t.exitPrice)}`}
            </p>
          </div>
          <div>
            <p className="text-gray-500 uppercase tracking-wider">Units</p>
            <p className="font-mono text-gray-200">{Math.abs(t.units).toLocaleString()}</p>
          </div>
          <div>
            <p className="text-gray-500 uppercase tracking-wider">At stop {t.stopLoss !== null && <span className="text-gray-600 normal-case">({px(t.stopLoss)})</span>}</p>
            <p className="font-mono font-semibold text-red-400">
              {t.projectedLossAud !== null ? aud(-t.projectedLossAud) : '—'}
            </p>
          </div>
          <div>
            <p className="text-gray-500 uppercase tracking-wider">At target {t.takeProfit !== null && <span className="text-gray-600 normal-case">({px(t.takeProfit)})</span>}</p>
            <p className="font-mono font-semibold text-emerald-400">
              {t.projectedProfitAud !== null ? aud(t.projectedProfitAud, true) : '—'}
              {rr !== null && <span className="text-gray-500 font-normal"> · {rr.toFixed(1)}R</span>}
            </p>
          </div>
        </div>

        <span className="text-[11px] text-gray-600">
          {open ? '▾ Hide' : '▸ How this size was reached'}
        </span>
      </button>

      {open && (
        <div className="border-t border-gray-800 px-4 py-3">
          {t.sizing ? <SizingBreakdown trade={t} /> : (
            <p className="text-xs text-gray-500 italic">
              No sizing record — this trade predates sizing transparency (or was placed outside TFG).
            </p>
          )}
        </div>
      )}
    </div>
  )
}

function SizingBreakdown({ trade: t }: { trade: TradeRecord }) {
  const s = t.sizing!
  const riskSourceLabel =
    s.riskSource === 'signal-override' ? 'from the webhook signal'
    : s.riskSource === 'db' ? 'from your risk settings'
    : 'config default'
  const stopSourceLabel = s.stopSource === 'signal-sl'
    ? 'stop-loss supplied by the signal'
    : `ATR ${s.atr} × ${s.stopSource.replace('atr×', '')} multiplier`
  const lossPerUnit = s.stopDistance * s.quoteToAud
  const rawUnits = lossPerUnit > 0 ? s.riskAmount / lossPerUnit : 0

  return (
    <div className="flex flex-col gap-1.5 text-xs font-mono">
      <Row label="Risk budget">
        {s.riskPercent}% × ${s.accountBalance.toLocaleString('en-AU', { minimumFractionDigits: 2 })} balance
        = <b className="text-emerald-400">${s.riskAmount.toLocaleString('en-AU', { minimumFractionDigits: 2 })} AUD</b>
        <span className="text-gray-600"> ({riskSourceLabel})</span>
      </Row>
      <Row label="Stop distance">
        {s.stopDistance.toFixed(5).replace(/0+$/, '').replace(/\.$/, '')} <span className="text-gray-600">({stopSourceLabel})</span>
      </Row>
      <Row label="FX conversion">
        quote→AUD {s.quoteToAud.toFixed(6)} → loss/unit {lossPerUnit.toFixed(6)} AUD
      </Row>
      <Row label="Raw size">
        ${s.riskAmount.toFixed(2)} ÷ {lossPerUnit.toFixed(6)} ≈ {Math.round(rawUnits).toLocaleString()} units
      </Row>
      <Row label="Final size">
        <b className="text-gray-100">{Math.abs(t.units).toLocaleString()} units</b>
        {s.capReason
          ? <span className="text-amber-400"> — reduced by {s.capReason === 'margin-cap' ? 'the 28% margin cap' : s.capReason}</span>
          : <span className="text-gray-600"> — no cap applied, full risk size</span>}
      </Row>
    </div>
  )
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex gap-2">
      <span className="text-gray-500 w-28 flex-shrink-0 uppercase tracking-wider text-[10px] pt-0.5">{label}</span>
      <span className="text-gray-300 min-w-0">{children}</span>
    </div>
  )
}
