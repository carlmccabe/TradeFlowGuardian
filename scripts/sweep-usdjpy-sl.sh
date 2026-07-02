#!/usr/bin/env bash
# Sweeps the TFG v5 USDJPY strategy across ATR stop-loss multipliers under the
# honest margin model (30:1, 40% utilisation cap), keeping the Pine TP:SL ratio
# (5.3 / 2.6 ≈ 2.04). Requires the Api running with historical M15 candles cached
# (run GET /api/backtest/data/coverage first).
#
# Usage:
#   ./scripts/sweep-usdjpy-sl.sh [API_BASE] [START_DATE] [END_DATE] [BALANCE]
#   ./scripts/sweep-usdjpy-sl.sh http://localhost:5000 2025-01-01 2026-06-30 100000
set -euo pipefail

API_BASE="${1:-http://localhost:5000}"
START="${2:-2025-01-01}"
END="${3:-2026-06-30}"
BALANCE="${4:-100000}"

# SL multipliers to test; TP = SL × 2.04 (Pine's 2.6→5.3 ratio)
SL_MULTS=(2.6 4.0 6.0 8.0)

command -v jq >/dev/null || { echo "jq is required"; exit 1; }

printf "\n%-8s %-8s %8s %10s %9s %8s %8s %10s\n" \
  "SL×ATR" "TP×ATR" "Trades" "Return" "MaxDD" "WinRate" "Sharpe" "PF"
printf '%.0s─' {1..75}; printf '\n'

for SL in "${SL_MULTS[@]}"; do
  TP=$(awk "BEGIN { printf \"%.1f\", $SL * 5.3 / 2.6 }")

  RESPONSE=$(curl -sS -X POST "$API_BASE/api/backtest/run" \
    -H "Content-Type: application/json" \
    -d @- <<JSON
{
  "name": "sweep USDJPY SL${SL}x TP${TP}x",
  "strategyPreset": "tfg_usdjpy_v5",
  "slMultiplier": $SL,
  "tpMultiplier": $TP,
  "instrument": "USD_JPY",
  "timeframe": "M15",
  "startDate": "${START}T00:00:00Z",
  "endDate": "${END}T00:00:00Z",
  "initialBalance": $BALANCE,
  "riskPerTrade": 0.025,
  "leverage": 30,
  "marginUtilisationLimit": 0.40,
  "maxPositionUnits": 1000000
}
JSON
  )

  if ! echo "$RESPONSE" | jq -e '.metrics' >/dev/null 2>&1; then
    echo "SL ${SL}x failed: $(echo "$RESPONSE" | jq -r '.error // .' 2>/dev/null | head -1)"
    continue
  fi

  echo "$RESPONSE" | jq -r --arg sl "$SL" --arg tp "$TP" '
    [$sl, $tp,
     (.metrics.totalTrades | tostring),
     ((.totalReturn * 100 | round * 0.01 | tostring) + "%"),
     ((.metrics.maxDrawdown * 100 | round * 0.01 | tostring) + "%"),
     ((.metrics.winRate * 100 | round | tostring) + "%"),
     (.metrics.sharpeRatio | . * 100 | round / 100 | tostring),
     (.metrics.profitFactor | . * 100 | round / 100 | tostring)
    ] | @tsv' |
  awk -F'\t' '{ printf "%-8s %-8s %8s %10s %9s %8s %8s %10s\n", $1, $2, $3, $4, $5, $6, $7, $8 }'
done

printf '\nEach run is saved — inspect trades via GET %s/api/backtest/runs\n' "$API_BASE"
