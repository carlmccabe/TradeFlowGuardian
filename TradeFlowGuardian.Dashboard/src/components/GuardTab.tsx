import { useCallback, useState } from 'react'
import { api, type RiskSettingsResponse, type PositionResponse } from '../api/client'
import { usePolling } from '../hooks/usePolling'
import { useSignalR, type TradingEvent } from '../hooks/useSignalR'
import { InstrumentCard } from './InstrumentCard'
import { SystemBar } from './SystemBar'

export function GuardTab() {
  const fetchStatus = useCallback(() => api.getStatus(), [])
  const { data: status, refresh: refreshStatus } = usePolling(fetchStatus, 30_000)

  const fetchRisk = useCallback(() => api.getRiskSettings(), [])
  const { data: riskData, refresh: refreshRisk } = usePolling(fetchRisk, 10_000)

  const fetchFilters = useCallback(() => api.getFilterStatus(), [])
  const { data: filters, refresh: refreshFilters } = usePolling(fetchFilters, 10_000)

  const [pauseAllBusy, setPauseAllBusy] = useState(false)

  const [riskSettings, setRiskSettings] = useState<RiskSettingsResponse[] | null>(null)
  const effectiveRisk = riskSettings ?? riskData ?? []

  function handleRiskUpdated(updated: RiskSettingsResponse) {
    setRiskSettings(prev => {
      const base = prev ?? riskData ?? []
      return base.map(r => r.instrument === updated.instrument ? updated : r)
    })
  }

  useSignalR(async (event: TradingEvent) => {
    if (event.type === 'order_filled' || event.type === 'position_closed') {
      await refreshStatus()
    }
    if (event.type === 'pause_changed' || event.type === 'drawdown_breached') {
      await refreshFilters()
    }
    if (event.type === 'risk_updated') {
      setRiskSettings(prev => {
        const base = prev ?? riskData ?? []
        return base.map(r => r.instrument === event.instrument
          ? { ...r, riskPercent: event.riskPercent, isActive: event.isActive }
          : r)
      })
    }
    if (event.type === 'risk_bulk_updated') {
      await refreshRisk()
      setRiskSettings(null)
    }
  })

  async function handlePauseAll() {
    setPauseAllBusy(true)
    try {
      await api.pauseAll()
      await refreshRisk()
      setRiskSettings(null)
    } finally {
      setPauseAllBusy(false)
    }
  }

  async function handleResumeAll() {
    setPauseAllBusy(true)
    try {
      await api.resumeAll()
      await refreshRisk()
      setRiskSettings(null)
    } finally {
      setPauseAllBusy(false)
    }
  }

  const anyInactive = effectiveRisk.some(r => !r.isActive)
  const positionMap: Record<string, PositionResponse> = {}
  status?.positions.forEach(p => { positionMap[p.instrument] = p })

  const dd = filters?.dailyDrawdown

  return (
    <div className="flex flex-col gap-4">
      {/* Balance + daily P&L */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-1">Account Balance</p>
        {status ? (
          <p className="text-3xl font-bold text-emerald-400">
            {status.balanceAud.toLocaleString('en-AU', { style: 'currency', currency: 'AUD', minimumFractionDigits: 2 })}
          </p>
        ) : (
          <p className="text-3xl font-bold text-gray-600 animate-pulse">—</p>
        )}
        {dd?.dayOpenNav != null && dd.drawdownPercent != null && (
          <p className={`text-xs mt-1 font-mono ${dd.isBreached ? 'text-red-400' : 'text-gray-500'}`}>
            Daily drawdown: {dd.drawdownPercent.toFixed(2)}% / {dd.maxDrawdownPercent}%
            {dd.isBreached && ' ⚠ BREACHED'}
          </p>
        )}
      </div>

      {/* Instrument cards */}
      <div className="flex flex-col gap-3">
        {effectiveRisk.length === 0 && (
          <p className="text-sm text-gray-500 italic px-1">Loading instruments…</p>
        )}
        {effectiveRisk.map(r => (
          <InstrumentCard
            key={r.instrument}
            settings={r}
            position={positionMap[r.instrument] ?? null}
            onUpdated={handleRiskUpdated}
          />
        ))}
      </div>

      {/* Macro pause button */}
      <button
        onClick={anyInactive ? handleResumeAll : handlePauseAll}
        disabled={pauseAllBusy}
        className={`w-full py-3 rounded-xl font-semibold text-sm transition-colors disabled:opacity-40 ${
          anyInactive
            ? 'bg-emerald-700 hover:bg-emerald-600 text-white'
            : 'bg-red-700 hover:bg-red-600 text-white'
        }`}
      >
        {pauseAllBusy
          ? 'Updating…'
          : anyInactive
          ? 'Resume All Instruments'
          : 'Pause All Instruments'}
      </button>

      {/* Deploy visibility + pipeline dry-run test */}
      <SystemBar />
    </div>
  )
}
