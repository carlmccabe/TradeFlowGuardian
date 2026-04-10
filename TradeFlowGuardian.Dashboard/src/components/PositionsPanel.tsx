import { useCallback, useState } from 'react'
import { api, type PositionResponse } from '../api/client'
import { usePolling } from '../hooks/usePolling'

function PositionRow({
  position,
  onClose,
}: {
  position: PositionResponse
  onClose: (instrument: string) => void
}) {
  const [closing, setClosing] = useState(false)
  const isLong = position.units > 0
  const plColor = position.unrealizedPL >= 0 ? 'text-emerald-400' : 'text-red-400'

  async function handleClose() {
    setClosing(true)
    try {
      await onClose(position.instrument)
    } finally {
      setClosing(false)
    }
  }

  return (
    <div className="flex items-center justify-between rounded-lg bg-gray-800 px-4 py-3 gap-4">
      <div className="flex items-center gap-3">
        <span
          className={`text-xs font-bold px-2 py-0.5 rounded ${
            isLong ? 'bg-emerald-900 text-emerald-300' : 'bg-red-900 text-red-300'
          }`}
        >
          {isLong ? 'LONG' : 'SHORT'}
        </span>
        <span className="font-mono font-medium text-gray-100">
          {position.instrument.replace('_', '/')}
        </span>
        <span className="text-sm text-gray-400">{Math.abs(position.units).toLocaleString()} units</span>
      </div>
      <div className="flex items-center gap-4">
        <span className={`font-mono font-semibold ${plColor}`}>
          {position.unrealizedPL >= 0 ? '+' : ''}
          {position.unrealizedPL.toFixed(2)}
        </span>
        <button
          onClick={handleClose}
          disabled={closing}
          className="text-xs font-semibold px-3 py-1.5 rounded bg-red-800 hover:bg-red-700 text-red-200 disabled:opacity-40 transition-colors"
        >
          {closing ? 'Closing…' : 'Close'}
        </button>
      </div>
    </div>
  )
}

export function PositionsPanel({ onClose }: { onClose: (instrument: string) => void }) {
  const fetcher = useCallback(() => api.getPositions(), [])
  const { data, error, loading, refresh } = usePolling(fetcher, 5_000)

  async function handleClose(instrument: string) {
    await onClose(instrument)
    refresh()
  }

  return (
    <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
      <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-3">
        Open Positions
      </p>
      {loading && <p className="text-sm text-gray-600 animate-pulse">Loading…</p>}
      {error && <p className="text-sm text-red-400">{error}</p>}
      {data && data.length === 0 && (
        <p className="text-sm text-gray-500 italic">No open positions</p>
      )}
      {data && data.length > 0 && (
        <div className="flex flex-col gap-2">
          {data.map((p) => (
            <PositionRow key={p.instrument} position={p} onClose={handleClose} />
          ))}
        </div>
      )}
    </div>
  )
}
