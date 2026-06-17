# Task: Cloudflare DNS + SSL

**Branch:** `develop`
**BACKLOG status to set:** `active` when you start, `done` when merged
**Scope:** Documentation and Railway custom domain configuration — no application code changes

---

## What to build

Document the process of pointing a custom domain at the Railway API service using
Cloudflare for DNS and automatic SSL. Update `docs/DEPLOYMENT.md` with a new section.

This is a documentation-and-verification task. The Railway and Cloudflare settings
are made in their respective dashboards — there is nothing to commit except docs.

---

## Context

Read `CLAUDE.md` and `docs/tasks/AGENT_INSTRUCTIONS.md` first.

### Current state

- Railway Api service has a public Railway-generated URL (e.g. `https://tradeflow-api-production.up.railway.app`)
- TradingView webhook URL currently uses this Railway URL
- No custom domain configured

### Goal state

- API reachable at `https://api.yourdomain.com` (or similar subdomain)
- Cloudflare acts as DNS provider and TLS termination proxy
- Railway handles the origin certificate
- TradingView webhook URL updated to the custom domain

### Architecture

```
TradingView alert
    ↓ HTTPS (port 443)
Cloudflare edge (DNS + TLS)
    ↓ HTTPS (Cloudflare origin cert)
Railway API service (custom domain configured)
    ↓
TradeFlowGuardian.Api container
```

---

## Files to read before starting

| File | Why |
|---|---|
| `docs/DEPLOYMENT.md` | Add new section here |
| `railway.toml` (root) | Confirm health check path — custom domain will use same health check |

---

## What to document

Add a `## Custom Domain (Cloudflare DNS + SSL)` section to `docs/DEPLOYMENT.md`
covering the following steps:

### Railway side

1. Railway dashboard → production environment → Api service → **Settings** → **Domains**
2. Click **+ Custom Domain**
3. Enter the desired subdomain (e.g. `api.yourdomain.com`)
4. Railway will show a CNAME record to add in Cloudflare — copy it

### Cloudflare side

1. Cloudflare dashboard → DNS → Add record:
   - Type: `CNAME`
   - Name: `api` (or the chosen subdomain)
   - Target: the Railway CNAME value (e.g. `xxx.railway.app`)
   - Proxy status: **Proxied** (orange cloud — enables Cloudflare TLS + DDoS protection)
2. SSL/TLS → Overview → Set mode to **Full** (not Full Strict — Railway's cert may be
   self-signed for the origin; Full mode terminates TLS at Cloudflare edge)

### Post-setup verification

```bash
curl https://api.yourdomain.com/api/signal/health
# → {"status":"ok","utc":"..."}

# Confirm TLS certificate issuer is Cloudflare
openssl s_client -connect api.yourdomain.com:443 -servername api.yourdomain.com \
  </dev/null 2>/dev/null | openssl x509 -noout -issuer
# issuer= ...Cloudflare...
```

### TradingView update

After the custom domain is live, update the TradingView alert webhook URL from:
```
https://tradeflow-api-production.up.railway.app/api/signal?secret=...
```
to:
```
https://api.yourdomain.com/api/signal?secret=...
```

### Staging custom domain (optional)

Same process for staging, using a different subdomain (e.g. `api-staging.yourdomain.com`)
pointed at the Railway staging Api service.

---

## Acceptance criteria

- [ ] `docs/DEPLOYMENT.md` has a new `## Custom Domain (Cloudflare DNS + SSL)` section
- [ ] Section covers: Railway custom domain setup, Cloudflare CNAME + proxy config, SSL mode, post-setup verification commands, TradingView URL update
- [ ] DEPLOYMENT.md mentions that the Railway-generated URL still works after custom domain is added (it does — Railway keeps both active)
- [ ] Section notes the Cloudflare SSL mode should be **Full** (not Full Strict, not Flexible)
- [ ] DEPLOYMENT.md updated in the staging environment note to suggest a staging subdomain
- [ ] `dotnet build` still passes (no C# changes should be needed, but verify)
- [ ] No secrets or real domain names committed to the docs — use `yourdomain.com` as placeholder

---

## Out of scope

- Purchasing a domain
- Setting up Cloudflare account
- WAF rules or rate limiting (separate concern)
- Email routing
- Wildcard certificates
- Changing any application code

---

## Gotchas

- Railway supports custom domains on the free and paid tiers — confirm the plan supports it.
- Cloudflare **Flexible** SSL mode sends unencrypted traffic to Railway origin — do NOT use Flexible.
  **Full** mode uses TLS to origin; **Full (Strict)** requires a valid CA-signed cert at origin
  (Railway may use self-signed). Use **Full**.
- If the health check fails after adding the custom domain, check that Cloudflare isn't
  caching the Railway error pages. Set Cache Rules → bypass for `/api/*` paths.
- TradingView webhooks send from fixed IP ranges — Cloudflare will proxy them through.
  This is fine; the `?secret=` param provides authentication.
- The Railway-generated URL (`*.up.railway.app`) remains valid — existing webhooks
  continue to work during the transition window.
