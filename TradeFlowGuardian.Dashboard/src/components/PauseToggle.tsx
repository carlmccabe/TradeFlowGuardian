import { useCallback, useState } from 'react'
import { api } from '../api/client'
import { usePolling } from '../hooks/usePolling'

export function PauseToggle() {
  const fetcher = useCallback(() => api.getFilterStatus(), [])
  const { data, refresh } = usePolling(fetcher, 10_000)
  const [loading, setLoading] = useState(false)

  const paused = data?.paused ?? false

  async function toggle() {
    setLoading(true)
    try {
      await api.setPaused(!paused)
      await refresh()
    } finally {
      setLoading(false)
    }
  }

  return (
    <button
      onClick={toggle}
      disabled={loading}
      className={`flex items-center gap-2 px-5 py-2.5 rounded-xl font-semibold text-sm transition-colors disabled:opacity-40 ${
        paused
          ? 'bg-emerald-700 hover:bg-emerald-600 text-white'
          : 'bg-red-700 hover:bg-red-600 text-white'
      }`}
    >
      <span className={`h-2 w-2 rounded-full ${paused ? 'bg-emerald-300' : 'bg-red-300'}`} />
      {loading ? 'Updating…' : paused ? 'Resume Trading' : 'Pause Trading'}
    </button>
  )
}
