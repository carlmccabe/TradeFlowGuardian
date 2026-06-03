interface TradeRow {
  instrument: string
  direction: string
  entryPrice: number
  exitPrice: number | null
  units: number
  openedAt: string
  closedAt: string | null
  pnlQuote: number | null
  durationSeconds: number | null
}

function formatDuration(secs: number | null): string {
  if (secs === null) return '—'
  if (secs < 60) return `${secs}s`
  if (secs < 3600) return `${Math.round(secs / 60)}m`
  return `${(secs / 3600).toFixed(1)}h`
}

export function TradeList({ trades }: { trades: TradeRow[] }) {
  if (trades.length === 0) {
    return <p className="text-sm text-gray-500 italic">No closed trades in selected period.</p>
  }

  return (
    <div className="flex flex-col gap-2">
      {trades.map((t, i) => {
        const isLong = t.direction === 'Long'
        const pnlColor = t.pnlQuote === null ? 'text-gray-500' : t.pnlQuote >= 0 ? 'text-emerald-400' : 'text-red-400'
        return (
          <div key={i} className="rounded-lg bg-gray-800 px-4 py-3 flex items-center justify-between gap-3 text-sm">
            <div className="flex items-center gap-2 min-w-0">
              <span className={`text-xs font-bold px-2 py-0.5 rounded flex-shrink-0 ${
                isLong ? 'bg-emerald-900 text-emerald-300' : 'bg-red-900 text-red-300'
              }`}>
                {t.direction.toUpperCase()}
              </span>
              <span className="font-mono text-gray-100 truncate">{t.instrument.replace('_', '/')}</span>
            </div>
            <div className="flex items-center gap-3 text-right flex-shrink-0">
              <div className="hidden sm:block">
                <p className="text-xs text-gray-500">Entry → Exit</p>
                <p className="font-mono text-gray-300">
                  {t.entryPrice.toFixed(t.entryPrice > 10 ? 3 : 5)}
                  {t.exitPrice !== null && ` → ${t.exitPrice.toFixed(t.exitPrice > 10 ? 3 : 5)}`}
                </p>
              </div>
              <div className="hidden sm:block">
                <p className="text-xs text-gray-500">Dur.</p>
                <p className="font-mono text-gray-300">{formatDuration(t.durationSeconds)}</p>
              </div>
              <div>
                <p className="text-xs text-gray-500">P&amp;L</p>
                <p className={`font-mono font-semibold ${pnlColor}`}>
                  {t.pnlQuote === null
                    ? 'open'
                    : `${t.pnlQuote >= 0 ? '+' : ''}${t.pnlQuote.toFixed(2)}`}
                </p>
              </div>
            </div>
          </div>
        )
      })}
    </div>
  )
}
