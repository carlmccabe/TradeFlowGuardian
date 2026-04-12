#!/usr/bin/env bash
# First-time secret setup — stores values in macOS Keychain.
# Run once. Values never touch the filesystem.
# Re-run to update a specific secret.
#
# Usage:
#   ./scripts/setup-secrets.sh            # set all secrets
#   ./scripts/setup-secrets.sh oanda-api-key  # update one

set -euo pipefail

SERVICE_PREFIX="tradeflow"

SECRETS=(
  "oanda-api-key:OANDA API Key"
  "oanda-account-id:OANDA Account ID"
  "webhook-secret:Webhook Secret"
)

store_secret() {
  local key="$1"
  local label="$2"

  # Check if already set
  local existing
  existing=$(security find-generic-password -a "$SERVICE_PREFIX" -s "$key" -w 2>/dev/null || true)

  if [[ -n "$existing" ]]; then
    echo -n "  $label already set. Update? [y/N] "
    read -r confirm
    [[ "$confirm" =~ ^[Yy]$ ]] || return 0
    security delete-generic-password -a "$SERVICE_PREFIX" -s "$key" 2>/dev/null || true
  fi

  echo -n "  Enter $label: "
  read -rs value
  echo

  if [[ -z "$value" ]]; then
    echo "  ⚠  Skipped (empty)"
    return 0
  fi

  security add-generic-password -a "$SERVICE_PREFIX" -s "$key" -w "$value"
  echo "  ✓  Stored in Keychain"
}

echo ""
echo "TradeFlow Guardian — Secret Setup"
echo "Secrets are stored in macOS Keychain, never on disk."
echo "────────────────────────────────────────────────────"
echo ""

TARGET="${1:-}"

for entry in "${SECRETS[@]}"; do
  key="${entry%%:*}"
  label="${entry##*:}"

  # If a specific key was requested, skip others
  [[ -n "$TARGET" && "$key" != "$TARGET" ]] && continue

  echo "→ $label"
  store_secret "$key" "$label"
  echo ""
done

echo "────────────────────────────────────────────────────"
echo "Done. Run ./scripts/dev.sh to start the stack."
echo ""
