import { useMemo } from 'react'
import type { BacktestEquityPoint } from '../api/client'

interface Props {
  curve: BacktestEquityPoint[]
  initialBalance: number
}

/** Evenly downsample to at most maxPoints so multi-year M15 curves stay light. */
function downsample(curve: BacktestEquityPoint[], maxPoints: number): BacktestEquityPoint[] {
  if (curve.length <= maxPoints) return curve
  const step = (curve.length - 1) / (maxPoints - 1)
  return Array.from({ length: maxPoints }, (_, i) => curve[Math.round(i * step)])
}

export function EquityChart({ curve, initialBalance }: Props) {
  const points = useMemo(() => downsample(curve, 300), [curve])

  const { path, baselineY, minLabel, maxLabel, endPositive } = useMemo(() => {
    const values = points.map(p => p.equity)
    const min = Math.min(...values, initialBalance)
    const max = Math.max(...values, initialBalance)
    const span = max - min || 1

    const W = 100
    const H = 40
    const x = (i: number) => (i / Math.max(points.length - 1, 1)) * W
    const y = (v: number) => H - ((v - min) / span) * H

    return {
      path: points.map((p, i) => `${i === 0 ? 'M' : 'L'}${x(i).toFixed(2)},${y(p.equity).toFixed(2)}`).join(' '),
      baselineY: y(initialBalance),
      minLabel: min,
      maxLabel: max,
      endPositive: values[values.length - 1] >= initialBalance,
    }
  }, [points, initialBalance])

  if (curve.length === 0) {
    return <p className="text-sm text-gray-500 italic">No equity data.</p>
  }

  return (
    <div>
      <div className="flex justify-between text-[10px] text-gray-600 mb-1">
        <span>{maxLabel.toLocaleString(undefined, { maximumFractionDigits: 0 })}</span>
      </div>
      <svg viewBox="0 0 100 40" preserveAspectRatio="none" className="w-full h-32">
        {/* starting balance baseline */}
        <line
          x1="0" y1={baselineY} x2="100" y2={baselineY}
          stroke="currentColor" strokeWidth="0.3" strokeDasharray="1.5,1.5"
          className="text-gray-600"
        />
        <path
          d={path}
          fill="none"
          stroke="currentColor"
          strokeWidth="0.6"
          vectorEffect="non-scaling-stroke"
          className={endPositive ? 'text-emerald-400' : 'text-red-400'}
        />
      </svg>
      <div className="flex justify-between text-[10px] text-gray-600 mt-1">
        <span>{minLabel.toLocaleString(undefined, { maximumFractionDigits: 0 })}</span>
        <span className="text-gray-500">
          {new Date(points[0].timestamp).toLocaleDateString('en-AU')} — {new Date(points[points.length - 1].timestamp).toLocaleDateString('en-AU')}
        </span>
      </div>
    </div>
  )
}
