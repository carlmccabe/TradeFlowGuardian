import { useCallback } from 'react'
import { api } from '../api/client'
import { usePolling } from '../hooks/usePolling'

export function HealthIndicator() {
  const fetcher = useCallback(() => api.getHealth(), [])
  const { data, error, loading } = usePolling(fetcher, 15_000)

  const isHealthy = !error && data?.status === 'ok'
  const isPending = loading && !data

  return (
    <div className="flex items-center gap-1.5" title={error ?? (isPending ? 'Checking…' : 'Connected')}>
      <span
        className={`h-2 w-2 rounded-full transition-colors ${
          isPending
            ? 'bg-gray-600'
            : isHealthy
            ? 'bg-emerald-400'
            : 'bg-red-500'
        }`}
      />
      <span className="text-xs text-gray-500 hidden sm:inline">
        {isPending ? 'Connecting…' : isHealthy ? 'Live' : 'Offline'}
      </span>
    </div>
  )
}
