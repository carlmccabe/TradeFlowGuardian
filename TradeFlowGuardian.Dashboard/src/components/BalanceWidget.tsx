import { useCallback } from 'react'
import { api } from '../api/client'
import { usePolling } from '../hooks/usePolling'

export function BalanceWidget() {
  const fetcher = useCallback(() => api.getBalance(), [])
  const { data, error, loading } = usePolling(fetcher, 10_000)

  return (
    <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
      <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-1">
        Account Balance
      </p>
      {loading && <p className="text-2xl font-bold text-gray-600 animate-pulse">—</p>}
      {error && <p className="text-sm text-red-400">{error}</p>}
      {data && (
        <p className="text-3xl font-bold text-emerald-400">
          {data.balanceAud.toLocaleString('en-AU', {
            style: 'currency',
            currency: 'AUD',
            minimumFractionDigits: 2,
          })}
        </p>
      )}
    </div>
  )
}
