import type { DailyPnlRecord } from '../api/client'

interface Props {
  bars: DailyPnlRecord[]
  weekly: boolean
}

function formatLabel(date: string, weekly: boolean): string {
  const d = new Date(date + 'T00:00:00Z')
  if (weekly) {
    return d.toLocaleDateString('en-AU', { month: 'short', day: 'numeric', timeZone: 'UTC' })
  }
  return d.toLocaleDateString('en-AU', { weekday: 'short', day: 'numeric', timeZone: 'UTC' })
}

export function PnlChart({ bars, weekly }: Props) {
  if (bars.length === 0) {
    return <p className="text-sm text-gray-500 italic">No closed trade history yet.</p>
  }

  const maxAbs = Math.max(...bars.map(b => Math.abs(b.pnl)), 1)
  const barMaxPx = 80

  return (
    <div className="flex items-end gap-1 h-24 w-full">
      {bars.map(bar => {
        const height = Math.max(3, (Math.abs(bar.pnl) / maxAbs) * barMaxPx)
        const positive = bar.pnl >= 0
        return (
          <div
            key={bar.date}
            className="flex flex-col items-center justify-end flex-1 gap-1 h-full group relative"
            title={`${bar.date}: ${bar.pnl >= 0 ? '+' : ''}${bar.pnl.toFixed(2)} (${bar.tradeCount} trade${bar.tradeCount !== 1 ? 's' : ''})`}
          >
            <div className="flex-1 flex items-end justify-center w-full">
              <div
                style={{ height: `${height}px` }}
                className={`w-full rounded-sm transition-opacity group-hover:opacity-80 ${
                  bar.pnl === 0 ? 'bg-gray-700' : positive ? 'bg-emerald-500' : 'bg-red-500'
                }`}
              />
            </div>
            {!weekly && (
              <span className="text-[10px] text-gray-600 leading-none">
                {formatLabel(bar.date, weekly)}
              </span>
            )}
          </div>
        )
      })}
    </div>
  )
}
