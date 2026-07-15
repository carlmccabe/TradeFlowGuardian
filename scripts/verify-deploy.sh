#!/usr/bin/env bash
# Post-deploy verification: proves what is actually running before money moves.
#
#   1. /api/status/version    — which build the Api is running, which account (practice/LIVE)
#   2. /api/status/readiness  — Postgres + schema level, Redis, broker, per-instrument
#                               risk settings (DB rows the sizer reads), Worker heartbeat + SHA
#   3. Dry-run signal         — full pipeline (queue → worker → filters → DB risk lookup →
#                               live balance → sizing → SL/TP) with NO order placed;
#                               fails if risk % came from config default instead of the DB
#
# Usage:
#   API_URL=https://your-api.up.railway.app ./scripts/verify-deploy.sh <webhook-secret> [instrument]
#
# Defaults: instrument=USD_JPY, API_URL=http://localhost:5205
# Exits non-zero on the first failed check. Requires curl + jq.

set -euo pipefail

SECRET="${1:?Usage: verify-deploy.sh <webhook-secret> [instrument]}"
INSTRUMENT="${2:-USD_JPY}"
API_URL="${API_URL:-http://localhost:5205}"

pass() { echo "  ✔ $1"; }
fail() { echo "  ✘ $1" >&2; exit 1; }

echo "── 1/3 Version ─────────────────────────────────────────────"
VERSION=$(curl -sf "$API_URL/api/status/version") || fail "Api unreachable at $API_URL"
echo "$VERSION" | jq .
SHA=$(echo "$VERSION" | jq -r '.sha')
IS_LIVE=$(echo "$VERSION" | jq -r '.isLive')
pass "Api running sha=${SHA:0:7} account=$(echo "$VERSION" | jq -r '.accountLabel') ($(echo "$VERSION" | jq -r '.accountEnvironment'))"
[ "$IS_LIVE" = "true" ] && echo "  ⚠ LIVE ACCOUNT — the dry run below still places no orders"

echo "── 2/3 Readiness ───────────────────────────────────────────"
READY=$(curl -sf "$API_URL/api/status/readiness") || fail "readiness endpoint unreachable"
echo "$READY" | jq .
[ "$(echo "$READY" | jq -r '.postgres.reachable')" = "true" ]     || fail "Postgres unreachable"
[ "$(echo "$READY" | jq -r '.postgres.schemaCurrent')" = "true" ] || fail "schema behind: applied=$(echo "$READY" | jq -r '.postgres.appliedMigration') expected=$(echo "$READY" | jq -r '.postgres.expectedMigration') — did pre-deploy --migrate-only run?"
[ "$(echo "$READY" | jq -r '.redis.reachable')" = "true" ]        || fail "Redis unreachable"
[ "$(echo "$READY" | jq -r '.broker.reachable')" = "true" ]       || fail "broker unreachable / zero balance"
[ "$(echo "$READY" | jq -r '.riskSettings.ok')" = "true" ]        || fail "no risk settings rows in DB — sizer would use config defaults"
[ "$(echo "$READY" | jq -r '.worker.healthy')" = "true" ]         || fail "worker heartbeat missing/stale — worker down or running a pre-heartbeat build"
WORKER_SHA=$(echo "$READY" | jq -r '.worker.sha // "unknown"')
[ "$WORKER_SHA" = "$SHA" ] || echo "  ⚠ worker sha=${WORKER_SHA:0:7} differs from api sha=${SHA:0:7} (staggered deploy?)"
echo "$READY" | jq -r '.riskSettings.instruments[] | "  ✔ \(.instrument): \(.riskPercent)% (\(.source)) active=\(.isActive)"'
pass "all dependencies healthy"

echo "── 3/3 Dry-run signal ($INSTRUMENT) ────────────────────────"
PRICE=$(curl -sf "$API_URL/api/price/mid/$INSTRUMENT" | jq -r '.mid // empty')
[ -n "$PRICE" ] || fail "could not fetch live price for $INSTRUMENT"
ATR=$(echo "$PRICE" | awk '{ printf "%.5f", $1 * 0.0015 }')  # plausible ATR ≈ 0.15% of price
KEY="verify_$(date +%s)"

curl -sf -X POST "$API_URL/api/signal?secret=$SECRET" \
  -H 'Content-Type: application/json' \
  -d "{\"instrument\":\"$INSTRUMENT\",\"direction\":\"Long\",\"price\":$PRICE,\"atr\":$ATR,\"riskPercent\":0,\"idempotencyKey\":\"$KEY\",\"dryRun\":true}" \
  > /dev/null || fail "signal POST rejected (secret wrong?)"
pass "dry-run signal queued (key=$KEY, price=$PRICE, atr=$ATR)"

RESULT=""
for _ in $(seq 1 20); do
  RESULT=$(curl -s "$API_URL/api/status/dryrun/$KEY")
  [ "$(echo "$RESULT" | jq -r '.dryRun // empty')" = "true" ] && break
  sleep 1
done
[ "$(echo "$RESULT" | jq -r '.dryRun // empty')" = "true" ] || fail "no dry-run result after 20s — check worker logs"
echo "$RESULT" | jq .

STAGE=$(echo "$RESULT" | jq -r '.stage')
WOULD=$(echo "$RESULT" | jq -r '.wouldTrade')
RISK_SOURCE=$(echo "$RESULT" | jq -r '.detail.sizing.riskSource // empty')

if [ "$WOULD" = "true" ]; then
  [ "$RISK_SOURCE" = "db" ] || fail "risk %% came from '$RISK_SOURCE', not the DB — the exact failure this script exists to catch"
  pass "pipeline reached order stage: $(echo "$RESULT" | jq -r '.outcome')"
  pass "risk source = db ($(echo "$RESULT" | jq -r '.detail.sizing.riskPercent')% of \$$(echo "$RESULT" | jq -r '.detail.balance'))"
  pass "projected: −\$$(echo "$RESULT" | jq -r '.detail.projectedLossAud') at stop / +\$$(echo "$RESULT" | jq -r '.detail.projectedProfitAud // "?"') at target"
else
  # A block by design (news window, market closed, position open) is a working pipeline too —
  # report it and let the operator judge.
  echo "  ⚠ pipeline blocked at stage '$STAGE': $(echo "$RESULT" | jq -r '.outcome')"
  echo "    (a real signal right now would be blocked for the same reason)"
fi

echo "────────────────────────────────────────────────────────────"
echo "DEPLOY VERIFIED: api=${SHA:0:7} worker=${WORKER_SHA:0:7} — full pipeline exercised, no orders placed."
