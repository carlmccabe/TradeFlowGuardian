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

# Compact JSON (remove whitespace) to match what the API will receive
COMPACT=$(echo "$BODY" | tr -d '\n' | sed 's/  */ /g' | sed 's/{ /{/g' | sed 's/ }/}/g' | sed 's/, /,/g' | sed 's/: /:/g')

SIG=$(echo -n "$COMPACT" | openssl dgst -sha256 -hmac "$SECRET" | awk '{print $2}')

echo "---------------------------------------"
echo "  Endpoint : $API_URL/api/signal"
echo "  Instrument: $INSTRUMENT"
echo "  Direction : $DIRECTION"
echo "  Idempotency: $IDEMPOTENCY_KEY"
echo "  Signature : sha256=$SIG"
echo "---------------------------------------"
echo "  Payload:"
echo "$COMPACT" | python3 -m json.tool 2>/dev/null || echo "$COMPACT"
echo "---------------------------------------"

RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_URL/api/signal" \
  -H "Content-Type: application/json" \
  -H "X-Signature: sha256=$SIG" \
  -d "$COMPACT")

HTTP_CODE=$(echo "$RESPONSE" | tail -1)
BODY_RESPONSE=$(echo "$RESPONSE" | head -n -1)

echo "  HTTP $HTTP_CODE"
echo "$BODY_RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$BODY_RESPONSE"
echo "---------------------------------------"

if [ "$HTTP_CODE" = "202" ]; then
  echo "  ✓ Signal accepted — check Worker logs for execution"
else
  echo "  ✗ Unexpected response — check API logs"
fi
