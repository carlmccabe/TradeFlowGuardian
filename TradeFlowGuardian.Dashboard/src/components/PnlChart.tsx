interface DayBar {
  label: string   // "Mon", "Tue", …
  date: string    // ISO date string
  pnl: number
}

interface WeekGroup {
  weekLabel: string  // "This week", "Jun 2", …
  days: DayBar[]
  total: number
}

interface Props {
  weeks: WeekGroup[]
  selectedWeek: number
  onSelectWeek: (i: number) => void
}

export function PnlChart({ weeks, selectedWeek, onSelectWeek }: Props) {
  if (weeks.length === 0) {
    return <p className="text-sm text-gray-500 italic">No trade data yet.</p>
  }

  const allPnl = weeks.flatMap(w => w.days.map(d => d.pnl))
  const maxAbs = Math.max(...allPnl.map(Math.abs), 1)
  const barMax = 80   // px height

  return (
    <div className="flex flex-col gap-4">
      {weeks.map((week, wi) => {
        const isSelected = wi === selectedWeek
        return (
          <button
            key={week.weekLabel}
            onClick={() => onSelectWeek(wi)}
            className={`w-full text-left rounded-xl border p-4 transition-colors ${
              isSelected ? 'border-emerald-700 bg-gray-900' : 'border-gray-800 bg-gray-900/60 hover:bg-gray-900'
            }`}
          >
            <div className="flex items-center justify-between mb-3">
              <span className="text-xs font-medium uppercase tracking-widest text-gray-500">
                {week.weekLabel}
              </span>
              <span className={`font-mono font-semibold text-sm ${week.total >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                {week.total >= 0 ? '+' : ''}{week.total.toFixed(2)}
              </span>
            </div>
            <div className="flex items-end gap-1.5 h-20">
              {week.days.map(day => {
                const height = Math.max(2, Math.abs(day.pnl / maxAbs) * barMax)
                const positive = day.pnl >= 0
                return (
                  <div key={day.date} className="flex flex-col items-center flex-1 gap-1">
                    <div className="flex-1 flex items-end justify-center w-full">
                      <div
                        style={{ height: `${height}px` }}
                        className={`w-full rounded-sm ${
                          day.pnl === 0
                            ? 'bg-gray-700'
                            : positive
                            ? 'bg-emerald-500'
                            : 'bg-red-500'
                        }`}
                      />
                    </div>
                    <span className="text-xs text-gray-600">{day.label}</span>
                  </div>
                )
              })}
            </div>
          </button>
        )
      })}
    </div>
  )
}

// ── helpers ──────────────────────────────────────────────────────────────────

export function buildWeekGroups(trades: { openedAt: string; pnlQuote: number }[]): WeekGroup[] {
  const now = new Date()
  const weeks: WeekGroup[] = []

  for (let w = 0; w < 5; w++) {
    const weekStart = new Date(now)
    weekStart.setUTCDate(now.getUTCDate() - now.getUTCDay() - 7 * w)
    weekStart.setUTCHours(0, 0, 0, 0)

    const weekEnd = new Date(weekStart)
    weekEnd.setUTCDate(weekStart.getUTCDate() + 7)

    const days: DayBar[] = [0, 1, 2, 3, 4].map(d => {
      const date = new Date(weekStart)
      date.setUTCDate(weekStart.getUTCDate() + 1 + d)  // Mon–Fri
      const dayTrades = trades.filter(t => {
        const opened = new Date(t.openedAt)
        return opened >= date && opened < new Date(date.getTime() + 86400_000)
      })
      return {
        label: date.toLocaleDateString('en-AU', { weekday: 'short' }),
        date: date.toISOString().slice(0, 10),
        pnl: dayTrades.reduce((s, t) => s + t.pnlQuote, 0),
      }
    })

    const monday = new Date(weekStart)
    monday.setUTCDate(weekStart.getUTCDate() + 1)
    const label = w === 0 ? 'This week' : monday.toLocaleDateString('en-AU', { month: 'short', day: 'numeric' })
    weeks.push({ weekLabel: label, days, total: days.reduce((s, d) => s + d.pnl, 0) })
  }

  return weeks
}
