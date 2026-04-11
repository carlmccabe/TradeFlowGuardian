import { useCallback } from 'react'
import { api, INSTRUMENTS, type PriceResponse } from '../api/client'
import { usePolling } from '../hooks/usePolling'

function PriceRow({ price }: { price: PriceResponse }) {
  return (
    <div className="flex items-center justify-between">
      <span className="font-mono text-sm text-gray-300">
        {price.instrument.replace('_', '/')}
      </span>
      <span className="font-mono text-sm font-semibold text-gray-100 tabular-nums">
        {price.mid.toFixed(price.instrument.endsWith('JPY') ? 3 : 5)}
      </span>
    </div>
  )
}

export function PriceTicker() {
  const fetcher = useCallback(() => api.getPrices(), [])
  const { data, error, loading } = usePolling(fetcher, 5_000)

  return (
    <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
      <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-3">
        Live Prices
      </p>
      {loading && <p className="text-sm text-gray-600 animate-pulse">Loading…</p>}
      {error && <p className="text-sm text-red-400">{error}</p>}
      {data && (
        <div className="flex flex-col gap-2">
          {data.map((p) => (
            <PriceRow key={p.instrument} price={p} />
          ))}
        </div>
      )}
      {!data && !loading && !error && (
        <div className="flex flex-col gap-2">
          {INSTRUMENTS.map((i) => (
            <div key={i} className="flex items-center justify-between">
              <span className="font-mono text-sm text-gray-500">{i.replace('_', '/')}</span>
              <span className="font-mono text-sm text-gray-700">—</span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
