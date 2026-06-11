import { useCallback, useEffect, useState } from 'react'
import { api, adminSecret, type AccountResponse, type CreateAccountRequest } from '../api/client'
import { usePolling } from '../hooks/usePolling'
import { useSignalR } from '../hooks/useSignalR'

export function AccountsTab() {
  const [secret, setSecret] = useState(adminSecret.get())
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const fetchActive = useCallback(() => api.getActiveAccount(), [])
  const { data: active, refresh: refreshActive } = usePolling(fetchActive, 30_000)

  const [accounts, setAccounts] = useState<AccountResponse[] | null>(null)

  const loadAccounts = useCallback(async () => {
    setError(null)
    try {
      setAccounts(await api.getAccounts())
    } catch (e) {
      setAccounts(null)
      setError(e instanceof Error ? e.message : 'Failed to load accounts')
    }
  }, [])

  useEffect(() => {
    if (adminSecret.get()) loadAccounts()
  }, [loadAccounts])

  useSignalR(async event => {
    if (event.type === 'account_changed') {
      await refreshActive()
      if (accounts) await loadAccounts()
    }
  })

  function handleSaveSecret() {
    adminSecret.set(secret.trim())
    loadAccounts()
  }

  async function handleActivate(account: AccountResponse) {
    if (account.environment === 'fxtrade') {
      const typed = window.prompt(
        `"${account.label}" is a LIVE account (${account.accountId}). Real money will be traded.\n\nType LIVE to confirm:`)
      if (typed !== 'LIVE') return
    } else if (!window.confirm(`Switch active account to "${account.label}" (${account.environment})?`)) {
      return
    }

    setBusy(true)
    setError(null)
    try {
      await api.activateAccount(account.id, account.environment === 'fxtrade')
      await Promise.all([loadAccounts(), refreshActive()])
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to switch account')
    } finally {
      setBusy(false)
    }
  }

  async function handleDelete(account: AccountResponse) {
    if (!window.confirm(`Delete account "${account.label}" (${account.accountId})? The stored API key is unrecoverable.`)) return
    setBusy(true)
    setError(null)
    try {
      await api.deleteAccount(account.id)
      await loadAccounts()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to delete account')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex flex-col gap-4">
      {/* Active account banner — public endpoint, always visible */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5">
        <p className="text-xs font-medium uppercase tracking-widest text-gray-500 mb-1">Trading On</p>
        {active ? (
          <div className="flex items-center gap-3">
            <p className="text-xl font-bold text-gray-100">{active.label}</p>
            <EnvBadge environment={active.environment} />
          </div>
        ) : (
          <p className="text-xl font-bold text-gray-600 animate-pulse">—</p>
        )}
        {active && <p className="text-xs text-gray-500 mt-1 font-mono">{active.accountId}</p>}
      </div>

      {/* Admin secret */}
      <div className="rounded-xl bg-gray-900 border border-gray-800 p-5 flex flex-col gap-2">
        <p className="text-xs font-medium uppercase tracking-widest text-gray-500">Admin Secret</p>
        <div className="flex gap-2">
          <input
            type="password"
            value={secret}
            onChange={e => setSecret(e.target.value)}
            placeholder="X-Admin-Secret"
            className="flex-1 bg-gray-950 border border-gray-800 rounded-lg px-3 py-2 text-sm text-gray-100 focus:outline-none focus:border-emerald-600"
          />
          <button
            onClick={handleSaveSecret}
            className="px-4 py-2 rounded-lg bg-emerald-700 hover:bg-emerald-600 text-white text-sm font-semibold"
          >
            Unlock
          </button>
        </div>
        <p className="text-[11px] text-gray-600">Same value as the webhook secret. Required to list or change accounts.</p>
      </div>

      {error && (
        <p className="text-sm text-red-400 bg-red-950/40 border border-red-900 rounded-lg px-3 py-2">{error}</p>
      )}

      {/* Registered accounts */}
      {accounts && (
        <div className="flex flex-col gap-3">
          {accounts.length === 0 && (
            <p className="text-sm text-gray-500 italic px-1">No accounts registered yet — add one below.</p>
          )}
          {accounts.map(a => (
            <div key={a.id} className="rounded-xl bg-gray-900 border border-gray-800 p-4 flex items-center justify-between gap-3">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <p className="font-semibold text-gray-100 truncate">{a.label}</p>
                  <EnvBadge environment={a.environment} />
                  {a.isActive && (
                    <span className="text-[10px] font-bold uppercase tracking-wider text-emerald-400 border border-emerald-800 rounded px-1.5 py-0.5">Active</span>
                  )}
                </div>
                <p className="text-xs text-gray-500 font-mono mt-1">{a.accountId}</p>
              </div>
              <div className="flex gap-2 shrink-0">
                {!a.isActive && (
                  <button
                    onClick={() => handleActivate(a)}
                    disabled={busy}
                    className="px-3 py-1.5 rounded-lg bg-gray-800 hover:bg-gray-700 text-emerald-400 text-xs font-semibold disabled:opacity-40"
                  >
                    Activate
                  </button>
                )}
                {!a.isActive && (
                  <button
                    onClick={() => handleDelete(a)}
                    disabled={busy}
                    className="px-3 py-1.5 rounded-lg bg-gray-800 hover:bg-red-900 text-red-400 text-xs font-semibold disabled:opacity-40"
                  >
                    Delete
                  </button>
                )}
              </div>
            </div>
          ))}

          <AddAccountForm
            busy={busy}
            onSubmit={async req => {
              setBusy(true)
              setError(null)
              try {
                await api.createAccount(req)
                await Promise.all([loadAccounts(), refreshActive()])
                return true
              } catch (e) {
                setError(e instanceof Error ? e.message : 'Failed to add account')
                return false
              } finally {
                setBusy(false)
              }
            }}
          />
        </div>
      )}
    </div>
  )
}

function AddAccountForm({ busy, onSubmit }: {
  busy: boolean
  onSubmit: (req: CreateAccountRequest) => Promise<boolean>
}) {
  const [label, setLabel] = useState('')
  const [accountId, setAccountId] = useState('')
  const [environment, setEnvironment] = useState<'fxpractice' | 'fxtrade'>('fxpractice')
  const [apiKey, setApiKey] = useState('')
  const [activate, setActivate] = useState(false)

  const valid = label.trim() && accountId.trim() && apiKey.trim()

  async function handleSubmit() {
    let confirmLive = false
    if (activate && environment === 'fxtrade') {
      const typed = window.prompt(
        `You are registering AND activating a LIVE account. Real money will be traded.\n\nType LIVE to confirm:`)
      if (typed !== 'LIVE') return
      confirmLive = true
    }
    const ok = await onSubmit({ label: label.trim(), accountId: accountId.trim(), environment, apiKey: apiKey.trim(), activate, confirmLive })
    if (ok) {
      setLabel(''); setAccountId(''); setApiKey(''); setActivate(false)
    }
  }

  const inputCls = 'bg-gray-950 border border-gray-800 rounded-lg px-3 py-2 text-sm text-gray-100 focus:outline-none focus:border-emerald-600'

  return (
    <div className="rounded-xl bg-gray-900 border border-gray-800 p-5 flex flex-col gap-3">
      <p className="text-xs font-medium uppercase tracking-widest text-gray-500">Add Account</p>
      <input className={inputCls} placeholder="Label (e.g. Demo — practice)" value={label} onChange={e => setLabel(e.target.value)} />
      <input className={`${inputCls} font-mono`} placeholder="OANDA account ID (e.g. 101-011-1234567-001)" value={accountId} onChange={e => setAccountId(e.target.value)} />
      <input className={`${inputCls} font-mono`} type="password" placeholder="API key (stored encrypted, never shown again)" value={apiKey} onChange={e => setApiKey(e.target.value)} />
      <div className="flex items-center gap-4">
        <select
          className={inputCls}
          value={environment}
          onChange={e => setEnvironment(e.target.value as 'fxpractice' | 'fxtrade')}
        >
          <option value="fxpractice">fxpractice (demo)</option>
          <option value="fxtrade">fxtrade (LIVE)</option>
        </select>
        <label className="flex items-center gap-2 text-sm text-gray-400">
          <input type="checkbox" checked={activate} onChange={e => setActivate(e.target.checked)} className="accent-emerald-600" />
          Activate now
        </label>
      </div>
      <button
        onClick={handleSubmit}
        disabled={busy || !valid}
        className="w-full py-2.5 rounded-lg bg-emerald-700 hover:bg-emerald-600 text-white text-sm font-semibold disabled:opacity-40"
      >
        {busy ? 'Saving…' : 'Add Account'}
      </button>
    </div>
  )
}

function EnvBadge({ environment }: { environment: string }) {
  const live = environment === 'fxtrade'
  return (
    <span className={`text-[10px] font-bold uppercase tracking-wider rounded px-1.5 py-0.5 border ${
      live
        ? 'text-red-400 border-red-800 bg-red-950/40'
        : 'text-sky-400 border-sky-900 bg-sky-950/40'
    }`}>
      {live ? 'LIVE' : 'DEMO'}
    </span>
  )
}
