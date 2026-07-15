import { useEffect, useRef, useState } from 'react'
import { api, type RiskSettingsResponse, type PositionResponse } from '../api/client'

const RISK_MIN = 0.5
const RISK_MAX = 3.0
const RISK_STEP = 0.1
// Taps apply locally right away; the PATCH fires once, this long after the last tap.
const RISK_COMMIT_DELAY_MS = 500

interface Props {
  settings: RiskSettingsResponse
  position: PositionResponse | null
  onUpdated: (updated: RiskSettingsResponse) => void
}

export function InstrumentCard({ settings, position, onUpdated }: Props) {
  const [busy, setBusy] = useState(false)
  const { instrument, riskPercent, isActive } = settings

  // Risk value shown before the server has confirmed it; null = in sync with settings.
  const [pendingRisk, setPendingRisk] = useState<number | null>(null)
  const commitTimer = useRef<number | undefined>(undefined)
  const commitSeq = useRef(0)
  const unflushed = useRef<number | null>(null)
  const displayRisk = pendingRisk ?? riskPercent

  const side = position
    ? position.units > 0 ? 'LONG' : 'SHORT'
    : 'FLAT'
  const sideColor = side === 'LONG'
    ? 'text-emerald-400 bg-emerald-950'
    : side === 'SHORT'
    ? 'text-red-400 bg-red-950'
    : 'text-gray-500 bg-gray-800'

  const plColor = position && position.unrealizedPL >= 0 ? 'text-emerald-400' : 'text-red-400'

  function adjustRisk(delta: number) {
    const next = Math.round((displayRisk + delta) * 10) / 10
    if (next < RISK_MIN || next > RISK_MAX) return
    setPendingRisk(next)
    unflushed.current = next
    window.clearTimeout(commitTimer.current)
    commitTimer.current = window.setTimeout(() => void commitRisk(next), RISK_COMMIT_DELAY_MS)
  }

  async function commitRisk(value: number) {
    const seq = ++commitSeq.current
    unflushed.current = null
    try {
      const updated = await api.updateRisk(instrument, value)
      if (seq === commitSeq.current) onUpdated(updated)
    } catch {
      // Revert to the last server-confirmed value; the 10s poll reconciles anyway.
    } finally {
      if (seq === commitSeq.current) {
        setPendingRisk(current => (current === value ? null : current))
      }
    }
  }

  // Tab switches unmount the card mid-debounce; don't lose the last taps.
  useEffect(() => () => {
    window.clearTimeout(commitTimer.current)
    if (unflushed.current !== null) void api.updateRisk(instrument, unflushed.current)
  }, [instrument])

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
            disabled={displayRisk <= RISK_MIN}
            className="w-7 h-7 rounded bg-gray-800 hover:bg-gray-700 text-gray-300 font-bold text-sm disabled:opacity-30 transition-colors"
          >
            −
          </button>
          <span className={`font-mono font-semibold w-10 text-center ${
            pendingRisk !== null ? 'text-amber-400' : 'text-emerald-400'
          }`}>
            {displayRisk.toFixed(1)}%
          </span>
          <button
            onClick={() => adjustRisk(+RISK_STEP)}
            disabled={displayRisk >= RISK_MAX}
            className="w-7 h-7 rounded bg-gray-800 hover:bg-gray-700 text-gray-300 font-bold text-sm disabled:opacity-30 transition-colors"
          >
            +
          </button>
        </div>
      </div>
    </div>
  )
}
