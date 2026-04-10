import { useEffect, useState, useCallback } from 'react'

export function usePolling<T>(
  fetcher: () => Promise<T>,
  intervalMs = 5000,
) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const fetch = useCallback(async () => {
    try {
      const result = await fetcher()
      setData(result)
      setError(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Unknown error')
    } finally {
      setLoading(false)
    }
  }, [fetcher])

  useEffect(() => {
    fetch()
    const id = setInterval(fetch, intervalMs)
    return () => clearInterval(id)
  }, [fetch, intervalMs])

  return { data, error, loading, refresh: fetch }
}
