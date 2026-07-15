import { useCallback, useState } from 'react'
import { api, adminSecret, type DryRunResult } from '../api/client'
import { usePolling } from '../hooks/usePolling'

/**
 * Deploy visibility strip: which build each service runs, whether every dependency
 * the trade pipeline needs is healthy, and a one-tap dry-run test that exercises
 * the full deployed pipeline without placing an order.
 */
export function SystemBar() {
  const fetcher = useCallback(() => api.getReadiness(), [])
  const { data: r, error } = usePolling(fetcher, 60_000)

  const [testing, setTesting] = useState(false)
  const [testResult, setTestResult] = useState<DryRunResult | null>(null)
  const [testError, setTestError] = useState<string | null>(null)

  async function runPipelineTest() {
    const secret = adminSecret.get()
    if (!secret) {
      setTestError('Set the admin secret in the Acct tab first — the test signal needs it.')
      return
    }
    setTesting(true)
    setTestResult(null)
    setTestError(null)
    try {
      const instrument = r?.riskSettings.instruments[0]?.instrument ?? 'USD_JPY'
      const { mid } = await api.getMidPrice(instrument)
      const atr = Math.round(mid * 0.0015 * 1e5) / 1e5 // plausible ATR ≈ 0.15% of price
      const key = `dashtest_${Date.now()}`
      await api.sendDryRunSignal(secret, instrument, mid, atr, key)
      // Poll for the worker's verdict (result lands in Redis, 404 until then).
      for (let i = 0; i < 20; i++) {
        await new Promise(res => setTimeout(res, 1000))
        try {
          setTestResult(await api.getDryRunResult(key))
          return
        } catch { /* not ready yet */ }
      }
      setTestError('No result after 20s — check worker logs.')
    } catch (e) {
      setTestError(e instanceof Error ? e.message : 'Test failed')
    } finally {
      setTesting(false)
    }
  }

  const sha7 = (s?: string | null) => (s && s !== 'unknown' ? s.slice(0, 7) : '???????')

  return (
    <div className="rounded-xl bg-gray-900 border border-gray-800 p-4 flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <p className="text-xs font-medium uppercase tracking-widest text-gray-500">System</p>
        {r && (
          <span className={`text-xs font-bold px-2 py-0.5 rounded ${
            r.ok ? 'bg-emerald-950 text-emerald-400' : 'bg-red-950 text-red-400'
          }`}>
            {r.ok ? 'ALL CHECKS PASS' : 'ATTENTION'}
          </span>
        )}
      </div>

      {error && <p className="text-xs text-red-400">{error}</p>}

      {r && (
        <div className="flex flex-col gap-1.5 text-xs font-mono">
          <CheckRow ok={r.broker.reachable}>
            <span className={`font-bold ${r.broker.isLive ? 'text-red-400' : 'text-emerald-400'}`}>
              {r.broker.isLive ? 'LIVE' : 'PRACTICE'}
            </span>
            <span className="text-gray-400"> {r.broker.accountLabel ?? 'no account'}</span>
            {r.broker.reachable && <span className="text-gray-500"> · ${r.broker.balanceAud.toFixed(2)}</span>}
          </CheckRow>
          <CheckRow ok={true}>api <b className="text-gray-200">{sha7(r.api.sha)}</b></CheckRow>
          <CheckRow ok={r.worker.healthy}>
            worker <b className="text-gray-200">{sha7(r.worker.sha)}</b>
            {r.worker.healthy
              ? <span className="text-gray-500"> · beat {Math.round(r.worker.beatAgeSeconds ?? 0)}s ago</span>
              : <span className="text-red-400"> · {r.worker.error ?? 'stale heartbeat'}</span>}
            {r.worker.sha && r.api.sha !== 'unknown' && r.worker.sha !== r.api.sha && (
              <span className="text-amber-400"> · SHA MISMATCH with api</span>
            )}
          </CheckRow>
          <CheckRow ok={r.postgres.schemaCurrent}>
            db schema {r.postgres.appliedMigration ?? '—'}/{r.postgres.expectedMigration ?? '—'}
            {!r.postgres.reachable && <span className="text-red-400"> · unreachable</span>}
            {r.postgres.reachable && !r.postgres.schemaCurrent && <span className="text-red-400"> · migration pending!</span>}
          </CheckRow>
          <CheckRow ok={r.riskSettings.ok}>
            risk from db:{' '}
            {r.riskSettings.ok
              ? r.riskSettings.instruments.map(i => `${i.instrument.replace('_', '/')} ${i.riskPercent}%`).join(' · ')
              : <span className="text-red-400">{r.riskSettings.note}</span>}
          </CheckRow>
        </div>
      )}

      <button
        onClick={runPipelineTest}
        disabled={testing || !r}
        className="w-full py-2 rounded-lg text-xs font-semibold bg-gray-800 hover:bg-gray-700 text-gray-300 disabled:opacity-40 transition-colors"
      >
        {testing ? 'Running dry-run through the live pipeline…' : 'Test pipeline (dry run — no order placed)'}
      </button>

      {testError && <p className="text-xs text-red-400">{testError}</p>}

      {testResult && (
        <div className={`rounded-lg p-3 text-xs font-mono border ${
          testResult.wouldTrade ? 'bg-emerald-950/40 border-emerald-900' : 'bg-amber-950/40 border-amber-900'
        }`}>
          <p className={testResult.wouldTrade ? 'text-emerald-400 font-bold' : 'text-amber-400 font-bold'}>
            {testResult.wouldTrade ? '✔ PIPELINE OK' : `⚠ BLOCKED at ${testResult.stage}`}
          </p>
          <p className="text-gray-300 mt-1">{testResult.outcome}</p>
          {testResult.detail?.sizing && (
            <p className="mt-1">
              <span className={testResult.detail.sizing.riskSource === 'db' ? 'text-emerald-400' : 'text-red-400'}>
                risk source: {testResult.detail.sizing.riskSource}
              </span>
              <span className="text-gray-400"> · {testResult.detail.sizing.riskPercent}% = ${testResult.detail.sizing.riskAmount?.toFixed(2)}</span>
            </p>
          )}
          {testResult.detail?.projectedLossAud != null && (
            <p className="text-gray-400">
              projected: <span className="text-red-400">−${testResult.detail.projectedLossAud.toFixed(2)}</span> at stop
              {testResult.detail.projectedProfitAud != null && (
                <> / <span className="text-emerald-400">+${testResult.detail.projectedProfitAud.toFixed(2)}</span> at target</>
              )}
            </p>
          )}
          <p className="text-gray-600 mt-1">worker {testResult.workerSha.slice(0, 7)} · {new Date(testResult.completedAt).toLocaleTimeString()}</p>
        </div>
      )}
    </div>
  )
}

function CheckRow({ ok, children }: { ok: boolean; children: React.ReactNode }) {
  return (
    <div className="flex items-baseline gap-2">
      <span className={ok ? 'text-emerald-400' : 'text-red-400'}>{ok ? '●' : '●'}</span>
      <span className="text-gray-300 min-w-0">{children}</span>
    </div>
  )
}
