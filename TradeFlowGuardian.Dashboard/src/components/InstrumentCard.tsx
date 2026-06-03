import { useState } from 'react'
import { api, type RiskSettingsResponse, type PositionResponse } from '../api/client'

const RISK_MIN = 0.5
const RISK_MAX = 3.0
const RISK_STEP = 0.1

interface Props {
  settings: RiskSettingsResponse
  position: PositionResponse | null
  onUpdated: (updated: RiskSettingsResponse) => void
}

export function InstrumentCard({ settings, position, onUpdated }: Props) {
  const [busy, setBusy] = useState(false)
  const { instrument, riskPercent, isActive } = settings

  const side = position
    ? position.units > 0 ? 'LONG' : 'SHORT'
    : 'FLAT'
  const sideColor = side === 'LONG'
    ? 'text-emerald-400 bg-emerald-950'
    : side === 'SHORT'
    ? 'text-red-400 bg-red-950'
    : 'text-gray-500 bg-gray-800'

  const plColor = position && position.unrealizedPL >= 0 ? 'text-emerald-400' : 'text-red-400'

  async function adjustRisk(delta: number) {
    const next = Math.round((riskPercent + delta) * 10) / 10
    if (next < RISK_MIN || next > RISK_MAX) return
    setBusy(true)
    try {
      const updated = await api.updateRisk(instrument, next)
      onUpdated(updated)
    } finally {
      setBusy(false)
    }
  }

  async function toggleActive() {
    setBusy(true)
    try {
      const updated = await api.updateRisk(instrument, undefined, !isActive)
      onUpdated(updated)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className={`rounded-xl border p-4 transition-colors ${
      isActive ? 'bg-gray-900 border-gray-700' : 'bg-gray-900/50 border-gray-800 opacity-60'
    }`}>
      {/* Header row */}
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          <span className="font-mono font-bold text-gray-100 text-base">
            {instrument.replace('_', '/')}
          </span>
          <span className={`text-xs font-bold px-2 py-0.5 rounded ${sideColor}`}>
            {side}
          </span>
        </div>
        <button
          onClick={toggleActive}
          disabled={busy}
          className={`text-xs font-semibold px-3 py-1 rounded-full transition-colors disabled:opacity-40 ${
            isActive
              ? 'bg-emerald-800 hover:bg-emerald-700 text-emerald-200'
              : 'bg-gray-700 hover:bg-gray-600 text-gray-400'
          }`}
        >
          {isActive ? 'Active' : 'Inactive'}
        </button>
      </div>

      {/* Position details */}
      {position ? (
        <div className="grid grid-cols-3 gap-2 mb-3 text-sm">
          <div>
            <p className="text-xs text-gray-500 uppercase tracking-wider mb-0.5">Entry</p>
            <p className="font-mono text-gray-200">{position.averagePrice.toFixed(position.averagePrice > 10 ? 3 : 5)}</p>
          </div>
          <div>
            <p className="text-xs text-gray-500 uppercase tracking-wider mb-0.5">Units</p>
            <p className="font-mono text-gray-200">{Math.abs(position.units).toLocaleString()}</p>
          </div>
          <div>
            <p className="text-xs text-gray-500 uppercase tracking-wider mb-0.5">Unreal. P&amp;L</p>
            <p className={`font-mono font-semibold ${plColor}`}>
              {position.unrealizedPL >= 0 ? '+' : ''}{position.unrealizedPL.toFixed(2)}
            </p>
          </div>
        </div>
      ) : (
        <p className="text-xs text-gray-600 italic mb-3">No open position</p>
      )}

      {/* Risk % stepper */}
      <div className="flex items-center justify-between">
        <span className="text-xs text-gray-500 uppercase tracking-wider">Risk</span>
        <div className="flex items-center gap-2">
          <button
            onClick={() => adjustRisk(-RISK_STEP)}
            disabled={busy || riskPercent <= RISK_MIN}
            className="w-7 h-7 rounded bg-gray-800 hover:bg-gray-700 text-gray-300 font-bold text-sm disabled:opacity-30 transition-colors"
          >
            −
          </button>
          <span className="font-mono text-emerald-400 font-semibold w-10 text-center">
            {riskPercent.toFixed(1)}%
          </span>
          <button
            onClick={() => adjustRisk(+RISK_STEP)}
            disabled={busy || riskPercent >= RISK_MAX}
            className="w-7 h-7 rounded bg-gray-800 hover:bg-gray-700 text-gray-300 font-bold text-sm disabled:opacity-30 transition-colors"
          >
            +
          </button>
        </div>
      </div>
    </div>
  )
}
