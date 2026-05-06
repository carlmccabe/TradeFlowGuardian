# Agent Instructions — TradeFlow Guardian

Read this file before starting any task. These rules are non-negotiable.

---

## 1. Read CLAUDE.md first

`/home/user/TradeFlowGuardian/CLAUDE.md` (or `CLAUDE.md` at repo root) contains:
- Architecture overview and project structure
- Conventions (IOptions<T>, no direct IConfiguration reads, no pyramiding, etc.)
- What NOT to do
- Tech debt register location

Read it before touching any code.

---

## 2. Branch rules

| Target | Branch |
|---|---|
| All development | `develop` |
| Production | `main` — never push directly |

```bash
git checkout develop
git pull origin develop   # always start from latest
```

Never commit to `main`. Never push to `main` directly.

---

## 3. Railway environments

| Railway environment | Branch | OANDA |
|---|---|---|
| staging | `develop` | fxpractice |
| production | `main` | fxpractice (or fxtrade) |

- Never modify Railway **production** environment variables, services, or plugins.
- Staging is the safety net — it deploys automatically when `develop` is pushed.
- If you break staging, fix it on `develop` before raising the PR.

---

## 4. Task lifecycle

When you **start** a task:
1. Move the task status in `docs/tasks/BACKLOG.md` from `todo` → `active`
2. Commit that change first so no two agents pick up the same task

When you **finish** a task:
1. Ensure all acceptance criteria in the task brief are checked
2. Run `dotnet build TradeFlowGuardian.sln` — must be clean (0 errors)
3. Run `dotnet test TradeFlowGuardian.Tests/ --no-build` — all tests must pass
4. For dashboard changes: `cd TradeFlowGuardian.Dashboard && npm run build` — must succeed
5. Move the task status in `docs/tasks/BACKLOG.md` from `active` → `done`
6. Update `docs/SESSION_LOG.md` with what was done (see existing entries for format)
7. Raise a PR: `develop` → `main` with a clear description

---

## 5. Code conventions (summary — CLAUDE.md is authoritative)

- Config via `IOptions<T>` — never read `IConfiguration` directly in services
- All OANDA calls go through `IOandaClient` — never call `HttpClient` directly
- DB schema changes go in `docs/migrations/` as numbered SQL files — no EF migrations
- Trade history writes must log-and-swallow — never surface to caller
- No pyramiding — one position per instrument
- Secrets never committed — no `.env` files, no `appsettings.Production.json`

---

## 6. Commit style

Use conventional commits:
```
feat(scope): short description
fix(scope): short description
docs(scope): short description
chore(scope): short description
```

Include the session URL as the last line of the commit body:
```
https://claude.ai/code/session_<id>
```

---

## 7. When in doubt

- Check `docs/TECH_DEBT.md` before starting — your task may overlap a known issue
- Check `docs/SESSION_LOG.md` for recent changes that might affect your work
- Do not add features beyond what the task brief specifies
- Do not refactor code outside the task scope
