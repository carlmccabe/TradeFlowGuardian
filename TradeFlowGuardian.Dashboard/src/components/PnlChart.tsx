export interface ChartBar {
  date: string        // "YYYY-MM-DD" (UTC)
  pnl: number
  tradeCount: number
  future: boolean     // day hasn't happened yet — render an empty slot
  isToday: boolean
  label: string       // x-axis tick ('' to suppress)
}

const HALF_PX = 56   // height of the positive (and negative) half, in px

function tooltip(b: ChartBar): string {
  if (b.future) return `${b.date}: upcoming`
  if (b.tradeCount === 0) return `${b.date}: no trades`
  const sign = b.pnl >= 0 ? '+' : ''
  return `${b.date}: ${sign}${b.pnl.toFixed(2)} (${b.tradeCount} trade${b.tradeCount !== 1 ? 's' : ''})`
}

/**
 * Diverging bar chart with a centered zero baseline: gains grow up (green),
 * losses grow down (red). Days with no trades sit flat on the line; future days
 * render as empty slots so the full week/month frame is always visible.
 */
export function PnlChart({ bars }: { bars: ChartBar[] }) {
  if (bars.length === 0) {
    return <p className="text-sm text-gray-500 italic">No closed trade history yet.</p>
  }

  const maxAbs = Math.max(...bars.map(b => Math.abs(b.pnl)), 1)

  return (
    <div>
      <div className="flex items-stretch gap-1">
        {bars.map(b => {
          const h = Math.max(2, Math.min(HALF_PX, (Math.abs(b.pnl) / maxAbs) * HALF_PX))
          const positive = !b.future && b.pnl > 0
          const negative = !b.future && b.pnl < 0
          const flat     = !b.future && b.pnl === 0
          return (
            <div
              key={b.date}
              className="flex-1 flex flex-col items-center group cursor-default"
              title={tooltip(b)}
            >
              {/* positive half — bars anchored to the zero line, growing up */}
              <div className="w-full flex items-end justify-center" style={{ height: HALF_PX }}>
                {positive && (
                  <div
                    style={{ height: h }}
                    className="w-full max-w-[20px] rounded-t-sm bg-emerald-500 transition-opacity group-hover:opacity-80"
                  />
                )}
              </div>

              {/* zero baseline */}
              <div className={`w-full h-px ${b.isToday ? 'bg-gray-500' : 'bg-gray-700'}`}>
                {flat && <div className="mx-auto w-1 h-1 -mt-0.5 rounded-full bg-gray-600" />}
              </div>

              {/* negative half — bars anchored to the zero line, growing down */}
              <div className="w-full flex items-start justify-center" style={{ height: HALF_PX }}>
                {negative && (
                  <div
                    style={{ height: h }}
                    className="w-full max-w-[20px] rounded-b-sm bg-red-500 transition-opacity group-hover:opacity-80"
                  />
                )}
              </div>
            </div>
          )
        })}
      </div>

      {/* x-axis labels */}
      <div className="flex gap-1 mt-1">
        {bars.map(b => (
          <span
            key={b.date}
            className={`flex-1 text-center text-[10px] leading-none ${
              b.isToday ? 'text-emerald-400 font-semibold' : 'text-gray-600'
            }`}
          >
            {b.label}
          </span>
        ))}
      </div>
    </div>
  )
}
