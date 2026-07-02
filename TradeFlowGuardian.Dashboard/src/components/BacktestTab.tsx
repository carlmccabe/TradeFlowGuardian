import { useCallback, useEffect, useState } from 'react'
import {
  api,
  type BacktestResultResponse,
  type BacktestRunSummary,
  type DataCoverageResponse,
} from '../api/client'
import { EquityChart } from './EquityChart'

const INSTRUMENTS = ['USD_JPY', 'EUR_USD', 'GBP_USD']
const TIMEFRAMES = ['M5', 'M15', 'M30', 'H1', 'H4', 'D']

function isoDate(d: Date): string {
  return d.toISOString().slice(0, 10)
}

function pct(v: number, digits = 1): string {
  return `${(v * 100).toFixed(digits)}%`
}

export function BacktestTab() {
  // ── form state ──────────────────────────────────────────────────────────
  const [presets, setPresets] = useState<string[]>(['tfg_usdjpy_v5'])
  const [preset, setPreset] = useState('tfg_usdjpy_v5')
  const [instrument, setInstrument] = useState('USD_JPY')
  const [timeframe, setTimeframe] = useState('M15')
  const [startDate, setStartDate] = useState(() => isoDate(new Date(Date.now() - 365 * 86400_000)))
  const [endDate, setEndDate] = useState(() => isoDate(new Date()))
  const [balance, setBalance] = useState(100_000)
  const [riskPct, setRiskPct] = useState(2.5)
  const [slMult, setSlMult] = useState(2.6)
  const [tpMult, setTpMult] = useState(5.3)
  const [fastPeriods, setFastPeriods] = useState(10)
  const [slowPeriods, setSlowPeriods] = useState(30)
  const [showAdvanced, setShowAdvanced] = useState(false)
  const [leverage, setLeverage] = useState(30)
  const [marginLimitPct, setMarginLimitPct] = useState(40)
  const [maxUnits, setMaxUnits] = useState(1_000_000)

  // ── run / result state ──────────────────────────────────────────────────
  const [running, setRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<BacktestResultResponse | null>(null)
  const [coverage, setCoverage] = useState<DataCoverageResponse | null>(null)
  const [runs, setRuns] = useState<BacktestRunSummary[] | null>(null)
  const [loadingRunId, setLoadingRunId] = useState<string | null>(null)

  const isTfg = preset.startsWith('tfg_')
  const isCustomEmac = preset === 'emac_custom'

  useEffect(() => {
    api.getBacktestStrategies().then(setPresets).catch(() => { /* keep fallback list */ })
  }, [])

  const refreshRuns = useCallback(() => {
    api.getBacktestRuns().then(setRuns).catch(() => setRuns([]))
  }, [])
  useEffect(refreshRuns, [refreshRuns])

  // Coverage check whenever the data window changes
  useEffect(() => {
    setCoverage(null)
    api.getDataCoverage(instrument, timeframe, startDate, endDate)
      .then(setCoverage)
      .catch(() => setCoverage(null))
  }, [instrument, timeframe, startDate, endDate])

  async function handleRun() {
    setRunning(true)
    setError(null)
    try {
      const res = await api.runBacktest({
        name: `${preset} ${instrument} ${timeframe} ${startDate}→${endDate}`,
        strategyPreset: preset,
        instrument,
        timeframe,
        startDate: `${startDate}T00:00:00Z`,
        endDate: `${endDate}T00:00:00Z`,
        initialBalance: balance,
        riskPerTrade: riskPct / 100,
        ...(isTfg ? { slMultiplier: slMult, tpMultiplier: tpMult } : {}),
        ...(isCustomEmac ? { fastPeriods, slowPeriods } : {}),
        leverage,
        marginUtilisationLimit: marginLimitPct / 100,
        maxPositionUnits: maxUnits,
      })
      setResult(res)
      refreshRuns()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Backtest failed')
    } finally {
      setRunning(false)
    }
  }

  async function handleLoadRun(id: string) {
    setLoadingRunId(id)
    setError(null)
    try {
      setResult(await api.getBacktestRun(id))
      window.scrollTo({ top: 0, behavior: 'smooth' })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load run')
    } finally {
      setLoadingRunId(null)
    }
  }

  return (
    <div className="flex flex-col gap-5">

      {/* ── Run form ─────────────────────────────────────────────────────── */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-4">
          Backtest
        </p>

        <div className="grid grid-cols-2 gap-3">
          <Field label="Strategy" className="col-span-2">
            <select value={preset} onChange={e => setPreset(e.target.value)} className={inputCls}>
              {presets.map(p => <option key={p} value={p}>{p}</option>)}
            </select>
          </Field>

          {isTfg && (
            <>
              <Field label="SL × ATR">
                <input type="number" step="0.1" min="0.1" value={slMult}
                  onChange={e => setSlMult(Number(e.target.value))} className={inputCls} />
              </Field>
              <Field label="TP × ATR">
                <input type="number" step="0.1" min="0.1" value={tpMult}
                  onChange={e => setTpMult(Number(e.target.value))} className={inputCls} />
              </Field>
            </>
          )}

          {isCustomEmac && (
            <>
              <Field label="Fast EMA">
                <input type="number" min="2" value={fastPeriods}
                  onChange={e => setFastPeriods(Number(e.target.value))} className={inputCls} />
              </Field>
              <Field label="Slow EMA">
                <input type="number" min="3" value={slowPeriods}
                  onChange={e => setSlowPeriods(Number(e.target.value))} className={inputCls} />
              </Field>
            </>
          )}

          <Field label="Instrument">
            <select value={instrument} onChange={e => setInstrument(e.target.value)} className={inputCls}>
              {INSTRUMENTS.map(i => <option key={i} value={i}>{i.replace('_', '/')}</option>)}
            </select>
          </Field>
          <Field label="Timeframe">
            <select value={timeframe} onChange={e => setTimeframe(e.target.value)} className={inputCls}>
              {TIMEFRAMES.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
          </Field>

          <Field label="From">
            <input type="date" value={startDate} onChange={e => setStartDate(e.target.value)} className={inputCls} />
          </Field>
          <Field label="To">
            <input type="date" value={endDate} onChange={e => setEndDate(e.target.value)} className={inputCls} />
          </Field>

          <Field label="Balance (AUD)">
            <input type="number" min="1000" step="1000" value={balance}
              onChange={e => setBalance(Number(e.target.value))} className={inputCls} />
          </Field>
          <Field label="Risk % / trade">
            <input type="number" min="0.1" max="50" step="0.1" value={riskPct}
              onChange={e => setRiskPct(Number(e.target.value))} className={inputCls} />
          </Field>
        </div>

        {/* Advanced: margin model */}
        <button
          onClick={() => setShowAdvanced(v => !v)}
          className="mt-3 text-xs text-gray-500 hover:text-gray-300 transition-colors"
        >
          {showAdvanced ? '▾' : '▸'} Margin model (mirrors live: {leverage}:1, {marginLimitPct}% cap)
        </button>
        {showAdvanced && (
          <div className="grid grid-cols-3 gap-3 mt-2">
            <Field label="Leverage">
              <input type="number" min="1" value={leverage}
                onChange={e => setLeverage(Number(e.target.value))} className={inputCls} />
            </Field>
            <Field label="Margin cap %">
              <input type="number" min="1" max="100" value={marginLimitPct}
                onChange={e => setMarginLimitPct(Number(e.target.value))} className={inputCls} />
            </Field>
            <Field label="Max units">
              <input type="number" min="1000" step="100000" value={maxUnits}
                onChange={e => setMaxUnits(Number(e.target.value))} className={inputCls} />
            </Field>
          </div>
        )}

        {/* Coverage hint */}
        {coverage && (
          <p className={`mt-3 text-xs ${coverage.isAvailable ? 'text-gray-500' : 'text-amber-400'}`}>
            Data coverage: {coverage.coveragePercent}% ({coverage.candlesFound.toLocaleString()} candles cached)
            {!coverage.isAvailable && ' — first run will fetch from OANDA and may take several minutes'}
          </p>
        )}

        <button
          onClick={handleRun}
          disabled={running}
          className="mt-4 w-full py-2.5 rounded-lg bg-emerald-700 hover:bg-emerald-600 disabled:opacity-40 text-emerald-50 font-semibold text-sm transition-colors"
        >
          {running ? 'Running… (may take 1–2 min)' : 'Run backtest'}
        </button>

        {error && (
          <p className="mt-3 text-sm text-red-400">
            {error}
            {error.includes('X-Admin-Secret') && (
              <span className="block mt-1 text-xs text-gray-500">
                Set the admin secret in the Acct tab first — backtest runs use the same secret.
              </span>
            )}
          </p>
        )}
      </div>

      {/* ── Result ───────────────────────────────────────────────────────── */}
      {result && <ResultPanel result={result} />}

      {/* ── Saved runs ───────────────────────────────────────────────────── */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-3">
          Saved Runs
        </p>
        {runs === null && <p className="text-sm text-gray-600 animate-pulse">Loading…</p>}
        {runs && runs.length === 0 && (
          <p className="text-sm text-gray-500 italic">No saved backtests yet.</p>
        )}
        {runs && runs.length > 0 && (
          <div className="flex flex-col gap-2">
            {runs.map(run => (
              <button
                key={run.id}
                onClick={() => handleLoadRun(run.id)}
                disabled={loadingRunId !== null}
                className="rounded-lg bg-gray-800 hover:bg-gray-750 px-4 py-3 text-left flex items-center justify-between gap-3 text-sm transition-colors disabled:opacity-50"
              >
                <div className="min-w-0">
                  <p className="text-gray-200 truncate">{run.name}</p>
                  <p className="text-xs text-gray-500">
                    {run.instrument.replace('_', '/')} {run.timeframe} · {run.totalTrades} trades ·{' '}
                    {new Date(run.createdAt).toLocaleDateString('en-AU')}
                  </p>
                </div>
                <span className={`font-mono font-semibold flex-shrink-0 ${
                  run.totalReturn >= 0 ? 'text-emerald-400' : 'text-red-400'
                }`}>
                  {loadingRunId === run.id ? '…' : `${run.totalReturn >= 0 ? '+' : ''}${pct(run.totalReturn)}`}
                </span>
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

// ── result panel ──────────────────────────────────────────────────────────────

function ResultPanel({ result }: { result: BacktestResultResponse }) {
  const m = result.metrics
  const [showAllTrades, setShowAllTrades] = useState(false)
  const trades = showAllTrades ? result.trades : result.trades.slice(-25)

  return (
    <div className="flex flex-col gap-5">
      {/* Headline + equity curve */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <div className="flex items-baseline justify-between mb-1">
          <p className="text-xs font-medium uppercase tracking-widest text-gray-500">Result</p>
          <p className="text-xs text-gray-600">{result.strategyName}</p>
        </div>
        <p className={`text-2xl font-bold ${result.totalReturn >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
          {result.totalReturn >= 0 ? '+' : ''}{pct(result.totalReturn)}
          <span className="text-sm font-normal text-gray-500 ml-2">
            {result.initialBalance.toLocaleString()} → {result.finalBalance.toLocaleString(undefined, { maximumFractionDigits: 0 })}
          </span>
        </p>
        <div className="mt-4">
          <EquityChart curve={result.equityCurve} initialBalance={result.initialBalance} />
        </div>
      </div>

      {/* Metrics grid */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-3">Metrics</p>
        <div className="grid grid-cols-3 gap-x-3 gap-y-4">
          <Metric label="Trades" value={`${m.totalTrades}`} />
          <Metric label="Win rate" value={pct(m.winRate)} good={m.winRate >= 0.5} bad={m.winRate < 0.35} />
          <Metric label="Max DD" value={pct(m.maxDrawdown)} bad={m.maxDrawdown > 0.15} />
          <Metric label="Profit factor" value={m.profitFactor.toFixed(2)} good={m.profitFactor >= 1.5} bad={m.profitFactor < 1} />
          <Metric label="Sharpe" value={m.sharpeRatio.toFixed(2)} good={m.sharpeRatio >= 1} bad={m.sharpeRatio < 0} />
          <Metric label="Expectancy" value={m.expectancyRatio.toFixed(2)} />
          <Metric label="Avg win" value={m.averageWin.toFixed(0)} />
          <Metric label="Avg loss" value={m.averageLoss.toFixed(0)} />
          <Metric label="Largest loss" value={m.largestLoss.toFixed(0)} />
        </div>
      </div>

      {/* Monthly breakdown */}
      {m.monthlyBreakdown.length > 0 && (
        <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
          <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-3">Monthly</p>
          <div className="flex flex-col gap-1.5">
            {m.monthlyBreakdown.map(mo => (
              <div key={mo.label} className="flex items-center justify-between text-sm">
                <span className="text-gray-400 font-mono">{mo.label}</span>
                <span className="text-xs text-gray-600">{mo.trades} trades · {pct(mo.winRate, 0)} win · {mo.averageR.toFixed(2)}R</span>
                <span className={`font-mono font-semibold ${mo.pnL >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                  {mo.pnL >= 0 ? '+' : ''}{mo.pnL.toFixed(0)}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Trades */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <div className="flex items-center justify-between mb-3">
          <p className="text-xs font-medium uppercase tracking-widest text-gray-500">
            Trades {!showAllTrades && result.trades.length > 25 && `(last 25 of ${result.trades.length})`}
          </p>
          {result.trades.length > 25 && (
            <button
              onClick={() => setShowAllTrades(v => !v)}
              className="text-xs text-gray-500 hover:text-gray-300 transition-colors"
            >
              {showAllTrades ? 'Show fewer' : 'Show all'}
            </button>
          )}
        </div>
        {trades.length === 0 && <p className="text-sm text-gray-500 italic">No trades in this run.</p>}
        <div className="flex flex-col gap-1.5">
          {trades.map(t => (
            <div key={t.tradeNumber} className="rounded-lg bg-gray-800 px-3 py-2 flex items-center justify-between gap-2 text-sm">
              <div className="flex items-center gap-2 min-w-0">
                <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded flex-shrink-0 ${
                  t.direction === 'Long' ? 'bg-emerald-900 text-emerald-300' : 'bg-red-900 text-red-300'
                }`}>
                  {t.direction === 'Long' ? 'L' : 'S'}
                </span>
                <span className="font-mono text-xs text-gray-400 truncate">
                  {new Date(t.entryTime).toLocaleDateString('en-AU')}{' '}
                  {t.entryPrice.toFixed(t.entryPrice > 10 ? 3 : 5)} → {t.exitPrice.toFixed(t.exitPrice > 10 ? 3 : 5)}
                </span>
                <span className="text-[10px] text-gray-600 flex-shrink-0">{t.exitReason}</span>
              </div>
              <div className="flex items-center gap-2 flex-shrink-0">
                {t.rMultiple !== null && (
                  <span className="text-[10px] text-gray-500 font-mono">{t.rMultiple.toFixed(1)}R</span>
                )}
                <span className={`font-mono font-semibold ${t.pnL >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                  {t.pnL >= 0 ? '+' : ''}{t.pnL.toFixed(0)}
                </span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

// ── small shared bits ─────────────────────────────────────────────────────────

const inputCls =
  'w-full rounded-lg bg-gray-800 border border-gray-700 px-3 py-2 text-sm text-gray-100 ' +
  'focus:outline-none focus:border-emerald-600 [color-scheme:dark]'

function Field({ label, children, className = '' }: {
  label: string
  children: React.ReactNode
  className?: string
}) {
  return (
    <label className={`flex flex-col gap-1 ${className}`}>
      <span className="text-xs text-gray-500">{label}</span>
      {children}
    </label>
  )
}

function Metric({ label, value, good, bad }: {
  label: string
  value: string
  good?: boolean
  bad?: boolean
}) {
  return (
    <div>
      <p className="text-xs text-gray-500">{label}</p>
      <p className={`font-mono font-semibold ${good ? 'text-emerald-400' : bad ? 'text-red-400' : 'text-gray-200'}`}>
        {value}
      </p>
    </div>
  )
}
