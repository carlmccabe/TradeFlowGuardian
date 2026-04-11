import { useCallback } from 'react'
import { api } from '../api/client'
import { usePolling } from '../hooks/usePolling'

function Indicator({
  label,
  active,
  activeColor,
  subtitle,
}: {
  label: string
  active: boolean
  activeColor: string
  subtitle?: string
}) {
  return (
    <div className="flex items-center gap-2">
      <span
        className={`h-2.5 w-2.5 rounded-full flex-shrink-0 ${
          active ? activeColor : 'bg-gray-700'
        }`}
      />
      <div>
        <span className={`text-sm ${active ? 'text-gray-100' : 'text-gray-500'}`}>
          {label}
        </span>
        {subtitle && (
          <p className="text-xs text-gray-500">{subtitle}</p>
        )}
      </div>
    </div>
  )
}

export function FilterStatus() {
  const fetcher = useCallback(() => api.getFilterStatus(), [])
  const { data, error, loading } = usePolling(fetcher, 10_000)

  const dd = data?.dailyDrawdown
  const drawdownSubtitle = dd?.dayOpenNav != null && dd?.drawdownPercent != null
    ? `${dd.drawdownPercent.toFixed(2)}% of ${dd.maxDrawdownPercent}% limit`
    : undefined

  return (
    <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
      <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-3">
        Filter Status
      </p>
      {loading && <p className="text-sm text-gray-600 animate-pulse">Loading…</p>}
      {error && <p className="text-sm text-red-400">{error}</p>}
      {data && (
        <div className="flex flex-col gap-3">
          <Indicator
            label="Paused"
            active={data.paused}
            activeColor="bg-red-500"
          />
          <Indicator
            label="Drawdown Limit Breached"
            active={dd?.isBreached ?? false}
            activeColor="bg-red-500"
            subtitle={drawdownSubtitle}
          />
        </div>
      )}
    </div>
  )
}
