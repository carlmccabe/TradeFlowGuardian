#!/usr/bin/env bash
# TradeFlow Guardian — dev runner
# Pulls secrets from macOS Keychain and starts the Docker Compose stack.
# Nothing is written to disk. Secrets exist only as env vars for the
# duration of this process.
#
# Usage:
#   ./scripts/dev.sh              # core stack (redis + api + worker)
#   ./scripts/dev.sh --full       # core + monitoring (grafana, prometheus, loki)
#   ./scripts/dev.sh --down       # stop and remove containers
#   ./scripts/dev.sh --logs       # tail logs without starting

set -euo pipefail

FULL=false
DOWN=false
LOGS=false

for arg in "$@"; do
  case "$arg" in
    --full)    FULL=true ;;
    --down)    DOWN=true ;;
    --logs)    LOGS=true ;;
    --help|-h)
      echo "Usage: dev.sh [--full] [--down] [--logs]"
      exit 0
      ;;
  esac
done

# ── Keychain helper ────────────────────────────────────────────────────────────
get_secret() {
  local key="$1"
  local value
  value=$(security find-generic-password -a tradeflow -s "$key" -w 2>/dev/null || true)
  if [[ -z "$value" ]]; then
    echo ""
    echo "  ✗ Secret '$key' not found in Keychain."
    echo "    Run: ./scripts/setup-secrets.sh"
    echo ""
    exit 1
  fi
  echo "$value"
}

# ── Down / Logs (no secrets needed) ───────────────────────────────────────────
if $DOWN; then
  echo "→ Stopping containers..."
  docker compose --profile core --profile monitoring down
  exit 0
fi

if $LOGS; then
  docker compose --profile core logs -f
  exit 0
fi

# ── Pull secrets from Keychain ─────────────────────────────────────────────────
echo ""
echo "TradeFlow Guardian — Dev Stack"
echo "Pulling secrets from Keychain..."

export OANDA_API_KEY=$(get_secret oanda-api-key)
export OANDA_ACCOUNT_ID=$(get_secret oanda-account-id)
export WEBHOOK_SECRET=$(get_secret webhook-secret)

echo "✓ Secrets loaded (not written to disk)"
echo ""

# ── Build profiles ─────────────────────────────────────────────────────────────
PROFILES="--profile core"
$FULL && PROFILES="$PROFILES --profile monitoring"

echo "→ Starting stack ($( $FULL && echo 'core + monitoring' || echo 'core only' ))..."
echo "  API:    http://localhost:8080/swagger"
$FULL && echo "  Grafana:   http://localhost:3000  (admin / tradeflow)"
$FULL && echo "  Prometheus: http://localhost:9090/targets"
$FULL && echo "  RedisInsight: http://localhost:5540"
echo ""

# ── Launch with watch (auto-rebuild on source changes) ────────────────────────
exec docker compose $PROFILES up --build --watch
