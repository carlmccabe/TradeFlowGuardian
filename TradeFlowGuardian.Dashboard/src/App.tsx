import { useState } from 'react'
import { AccountsTab } from './components/AccountsTab'
import { GuardTab } from './components/GuardTab'
import { PnlTab } from './components/PnlTab'
import { TradesTab } from './components/TradesTab'

type Tab = 'guard' | 'trades' | 'pnl' | 'accounts'

export default function App() {
  const [tab, setTab] = useState<Tab>('guard')

  return (
    <div className="min-h-screen bg-gray-950 text-gray-100 font-mono">
      {/* Header */}
      <header className="border-b border-gray-800 px-4 py-3 flex items-center justify-between sticky top-0 bg-gray-950/90 backdrop-blur z-10">
        <div className="flex items-center gap-2">
          <span className="text-emerald-400 font-bold text-lg tracking-tight">TradeFlow</span>
          <span className="text-gray-500 text-lg font-light">Guardian</span>
        </div>
        {/* Tab switcher */}
        <div className="flex bg-gray-900 rounded-lg p-1 gap-1">
          <TabButton active={tab === 'guard'} onClick={() => setTab('guard')}>Guard</TabButton>
          <TabButton active={tab === 'trades'} onClick={() => setTab('trades')}>Trades</TabButton>
          <TabButton active={tab === 'pnl'}   onClick={() => setTab('pnl')}>P&amp;L</TabButton>
          <TabButton active={tab === 'accounts'} onClick={() => setTab('accounts')}>Acct</TabButton>
        </div>
      </header>

      {/* Main */}
      <main className="max-w-2xl mx-auto px-4 py-6">
        {tab === 'guard' ? <GuardTab /> : tab === 'trades' ? <TradesTab /> : tab === 'pnl' ? <PnlTab /> : <AccountsTab />}
      </main>
    </div>
  )
}

function TabButton({ active, onClick, children }: {
  active: boolean
  onClick: () => void
  children: React.ReactNode
}) {
  return (
    <button
      onClick={onClick}
      className={`px-4 py-1.5 rounded-md text-sm font-semibold transition-colors ${
        active
          ? 'bg-gray-800 text-emerald-400'
          : 'text-gray-500 hover:text-gray-300'
      }`}
    >
      {children}
    </button>
  )
}
