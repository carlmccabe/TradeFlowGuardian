# Task Backlog — TradeFlow Guardian

Managed by CC agents. Each task has a brief in `docs/tasks/active/`.
Update status here when picking up or completing a task.

---

## Status key

| Status | Meaning |
|---|---|
| `todo` | Ready to be picked up |
| `active` | An agent is working on it — check the brief for branch/PR |
| `done` | Merged to `main` |

---

## Tasks

| # | Task | Brief | Status | Notes |
|---|---|---|---|---|
| 1 | P&L chart (daily/weekly) in React dashboard | [pnl-chart.md](active/pnl-chart.md) | `todo` | Needs new API endpoint + Recharts component |
| 2 | SignalR hub replacing dashboard polling | [signalr-hub.md](active/signalr-hub.md) | `todo` | Phase 3 final — do after P&L chart |
| 3 | EUR/USD Pine Script | [eurusd-pine-script.md](active/eurusd-pine-script.md) | `todo` | Pine-only, no C# changes |
| 4 | GBP/USD Pine Script | [gbpusd-pine-script.md](active/gbpusd-pine-script.md) | `todo` | Pine-only, no C# changes |
| 5 | GitHub Actions CI/CD | [github-actions-cicd.md](active/github-actions-cicd.md) | `todo` | Build + test on PR; Railway handles deploy |
| 6 | Cloudflare DNS + SSL | [cloudflare-dns-ssl.md](active/cloudflare-dns-ssl.md) | `todo` | Docs + Railway custom domain config |
| 7 | Redis IOptions tech debt | [redis-ioptions-tech-debt.md](active/redis-ioptions-tech-debt.md) | `todo` | 2-line fix in Api + Worker Program.cs |

---

## Completed

| # | Task | Merged | Notes |
|---|---|---|---|
| — | Pine Script signal payload fix | 2026-05-06 | `pine/usdjpy_emac_signal.pine` created |
| — | Close position OANDA fix | 2026-05-06 | Pre-query position before PUT close |
| — | Staging environment setup | 2026-05-06 | `develop` branch, DEPLOYMENT.md updated |
