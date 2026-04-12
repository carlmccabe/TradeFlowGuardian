#!/usr/bin/env bash
# End-to-end webhook test against the local API.
# Usage:
#   ./scripts/test-signal.sh <webhook-secret> [direction] [instrument]
#
# Defaults: direction=Long, instrument=USD_JPY
# Directions: Long | Short | Close
#
# Example:
#   ./scripts/test-signal.sh mysecret Long USD_JPY

set -euo pipefail

SECRET="${1:?Usage: test-signal.sh <webhook-secret> [direction] [instrument]}"
DIRECTION="${2:-Long}"
INSTRUMENT="${3:-USD_JPY}"
API_URL="${API_URL:-http://localhost:5205}"

TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
IDEMPOTENCY_KEY="${INSTRUMENT//_/}_$(date +%s)"

BODY=$(cat <<EOF
{
  "instrument": "$INSTRUMENT",
  "direction": "$DIRECTION",
  "atr": 0.245,
  "price": 149.500,
  "riskPercent": 2.5,
  "timestamp": "$TIMESTAMP",
  "idempotencyKey": "$IDEMPOTENCY_KEY"
}
EOF
)

# Compact JSON
COMPACT=$(echo "$BODY" | python3 -c "import sys,json; print(json.dumps(json.loads(sys.stdin.read()), separators=(',', ':')), end='')")

echo "---------------------------------------"
echo "  Endpoint : $API_URL/api/signal?secret=***"
echo "  Instrument: $INSTRUMENT"
echo "  Direction : $DIRECTION"
echo "  Idempotency: $IDEMPOTENCY_KEY"
echo "---------------------------------------"
echo "  Payload:"
echo "$COMPACT" | python3 -m json.tool 2>/dev/null || echo "$COMPACT"
echo "---------------------------------------"

TMPFILE=$(mktemp)
HTTP_CODE=$(curl -s -o "$TMPFILE" -w "%{http_code}" -X POST "$API_URL/api/signal?secret=$SECRET" \
  -H "Content-Type: application/json" \
  -d "$COMPACT")
BODY_RESPONSE=$(cat "$TMPFILE")
rm -f "$TMPFILE"

echo "  HTTP $HTTP_CODE"
echo "$BODY_RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$BODY_RESPONSE"
echo "---------------------------------------"

if [ "$HTTP_CODE" = "202" ]; then
  echo "  ✓ Signal accepted — check Worker logs for execution"
else
  echo "  ✗ Unexpected response — check API logs"
fi
