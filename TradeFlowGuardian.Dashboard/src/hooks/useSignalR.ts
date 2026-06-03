import { useEffect, useRef, useCallback } from 'react'
import * as signalR from '@microsoft/signalr'

const HUB_URL = import.meta.env.VITE_API_URL
  ? `${import.meta.env.VITE_API_URL}/hubs/trading`
  : '/hubs/trading'

export type TradingEvent =
  | { type: 'order_filled'; instrument: string; direction: string; units: number; entryPrice: number; stopLoss: number; takeProfit: number; orderId: string; unrealisedPnl: number; status: string }
  | { type: 'position_closed'; instrument: string; exitPrice: number; orderId: string; status: string }
  | { type: 'risk_updated'; instrument: string; riskPercent: number; isActive: boolean }
  | { type: 'risk_bulk_updated'; isActive: boolean }

export function useSignalR(onEvent: (event: TradingEvent) => void) {
  const connRef = useRef<signalR.HubConnection | null>(null)
  const onEventRef = useRef(onEvent)
  onEventRef.current = onEvent

  const connect = useCallback(async () => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conn.on('event', (raw: string) => {
      try {
        const event = JSON.parse(raw) as TradingEvent
        onEventRef.current(event)
      } catch {
        // malformed event — ignore
      }
    })

    try {
      await conn.start()
    } catch {
      // Hub unreachable at startup — polling fallback handles live data
    }

    connRef.current = conn
  }, [])

  useEffect(() => {
    connect()
    return () => {
      connRef.current?.stop()
    }
  }, [connect])
}
