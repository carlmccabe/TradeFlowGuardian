import { api } from './api/client'
import { BalanceWidget } from './components/BalanceWidget'
import { FilterStatus } from './components/FilterStatus'
import { PauseToggle } from './components/PauseToggle'
import { PositionsPanel } from './components/PositionsPanel'

export default function App() {
  async function handleClose(instrument: string) {
    await api.closePosition(instrument)
  }

  return (
    <div className="min-h-screen bg-gray-950 text-gray-100">
      {/* Header */}
      <header className="border-b border-gray-800 px-4 py-3 flex items-center justify-between sticky top-0 bg-gray-950/90 backdrop-blur z-10">
        <div className="flex items-center gap-2">
          <span className="text-emerald-400 font-bold text-lg tracking-tight">TradeFlow</span>
          <span className="text-gray-500 text-lg font-light">Guardian</span>
        </div>
        <PauseToggle />
      </header>

      {/* Main */}
      <main className="max-w-2xl mx-auto px-4 py-6 flex flex-col gap-4">
        <BalanceWidget />
        <PositionsPanel onClose={handleClose} />
        <FilterStatus />
      </main>
    </div>
  )
}
