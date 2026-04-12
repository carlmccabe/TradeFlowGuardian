# Secrets Management — TradeFlow Guardian

## Threat Model

A compromised AI tool or process with shell/filesystem access attempts to silently
exfiltrate credentials. The goal is to ensure secrets cannot be read without
explicit human approval (system dialog or biometric).

| Storage Method | Safe from Compromised Tool? | Notes |
|---|---|---|
| `.env` file | ❌ No | Plaintext, readable by any process |
| `dotnet user-secrets` | ❌ No | Plaintext JSON in `~/Library` |
| Keychain (default) | ⚠️ Partial | CLI-readable without prompting |
| **Keychain + ACL (`-T ""`)** | ✅ Yes | System dialog required on every read |
| **1Password CLI** | ✅ Yes | Touch ID / master password per session |

---

## Current Setup — Keychain with ACL (paper account)

Secrets are stored in macOS Keychain with strict access control. Any process
that attempts to read a secret — including AI tools — triggers a system
confirmation dialog requiring your Mac password or Touch ID.

### First-time setup

```bash
./scripts/setup-secrets.sh
```

Prompts interactively for each secret and stores with `-T ""` ACL. Never writes
to disk.

### Updating a single secret

```bash
./scripts/setup-secrets.sh oanda-api-key
```

### Manually storing with ACL (bypassing the script)

```bash
# Delete existing entry first if present
security delete-generic-password -a tradeflow -s oanda-api-key 2>/dev/null

# Store with strict ACL — forces system dialog on every read
security add-generic-password -a tradeflow -s oanda-api-key    -w "your-key"  -T ""
security add-generic-password -a tradeflow -s oanda-account-id -w "your-id"   -T ""
security add-generic-password -a tradeflow -s webhook-secret   -w "your-secret" -T ""
```

### Reading a secret (triggers system dialog)

```bash
security find-generic-password -a tradeflow -s oanda-api-key -w
```

### Viewing / editing secrets

Open **Keychain Access.app** → search "tradeflow" → double-click any entry.

### Deleting a secret

```bash
security delete-generic-password -a tradeflow -s oanda-api-key
```

### Starting the dev stack

```bash
./scripts/dev.sh           # core (redis + api + worker)
./scripts/dev.sh --full    # + grafana + prometheus + loki + redis-insight
./scripts/dev.sh --down    # stop everything
./scripts/dev.sh --logs    # tail logs
```

Each run reads secrets from Keychain (triggering system dialogs) and passes
them as environment variables to Docker. Nothing is written to disk.

---

## Go-Live Setup — 1Password CLI (live account)

When switching to a live OANDA account, use 1Password CLI. Secrets never
touch the dev machine — they are injected at runtime from the vault.

### Install

```bash
brew install 1password-cli
```

### Sign in (once per terminal session, requires Touch ID)

```bash
op signin
```

### Store secrets in 1Password

Create a vault named `TradeFlow` in the 1Password app, then add items:

| Item name | Field | Value |
|---|---|---|
| `OANDA` | `api-key` | Live API key |
| `OANDA` | `account-id` | Live account ID |
| `Webhook` | `secret` | Webhook secret token |

### Read a secret

```bash
op read "op://TradeFlow/OANDA/api-key"
```

### Use with dev.sh (1Password mode)

Update `dev.sh` or create `dev-live.sh` replacing the Keychain block with:

```bash
eval $(op signin)
export OANDA_API_KEY=$(op read "op://TradeFlow/OANDA/api-key")
export OANDA_ACCOUNT_ID=$(op read "op://TradeFlow/OANDA/account-id")
export WEBHOOK_SECRET=$(op read "op://TradeFlow/Webhook/secret")
```

A compromised tool calling `op read` will be blocked — 1Password requires
your explicit Touch ID or master password approval.

---

## Production — Azure Key Vault (live deployment)

Live credentials should **never exist on the dev machine**. In production
(Azure Container Apps), secrets are injected from Azure Key Vault at
container startup. Your laptop never sees them.

### Store secrets in Key Vault

```bash
az keyvault secret set --vault-name tradeflow-kv --name oanda-api-key    --value "your-live-key"
az keyvault secret set --vault-name tradeflow-kv --name oanda-account-id --value "your-live-id"
az keyvault secret set --vault-name tradeflow-kv --name webhook-secret   --value "your-secret"
```

### Reference in Azure Container Apps

```bash
# Register the secret reference
az containerapp secret set --name tradeflow-api \
  --secrets "oanda-api-key=keyvaultref:https://tradeflow-kv.vault.azure.net/secrets/oanda-api-key,identityref:/subscriptions/.../managedIdentities/tradeflow"

# Map to environment variable
az containerapp update --name tradeflow-api \
  --set-env-vars "Oanda__ApiKey=secretref:oanda-api-key"
```

### Key principle

```
Dev machine  →  paper account only  (Keychain ACL or 1Password)
Azure ACA    →  live account only   (Azure Key Vault — laptop never sees it)
```

Even full compromise of the dev machine cannot expose live trading credentials.

---

## Secret Names Reference

| Keychain key | 1Password path | Azure Key Vault name | .NET config key |
|---|---|---|---|
| `oanda-api-key` | `op://TradeFlow/OANDA/api-key` | `oanda-api-key` | `Oanda:ApiKey` |
| `oanda-account-id` | `op://TradeFlow/OANDA/account-id` | `oanda-account-id` | `Oanda:AccountId` |
| `webhook-secret` | `op://TradeFlow/Webhook/secret` | `webhook-secret` | `Webhook:Secret` |

---

## What is Gitignored

The following are blocked from ever being committed:

```
.env  .env.*  *.env  secrets.env  docker.env  .envrc
**/appsettings.Development.json
**/appsettings.*.json   (only appsettings.json is allowed)
```
