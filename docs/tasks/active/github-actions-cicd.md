# Task: GitHub Actions CI/CD

**Branch:** `develop`
**BACKLOG status to set:** `active` when you start, `done` when merged

---

## What to build

Two GitHub Actions workflows:

1. **`ci.yml`** — runs on every PR targeting `develop` or `main`:
   - `dotnet build` (all projects)
   - `dotnet test` (xunit suite)
   - `npm run build` (dashboard)

2. **`deploy-staging.yml`** — informational only (Railway deploys automatically on push
   to `develop`). This workflow just posts a comment on the PR confirming the staging
   URL once CI is green. Railway does the actual deploy — no deployment step needed here.

There is no third workflow for production — Railway auto-deploys `main` on push.
The CI workflow acts as the gate: merging to `main` requires a green CI run.

---

## Context

Read `CLAUDE.md` and `docs/tasks/AGENT_INSTRUCTIONS.md` first.

### Repo structure

```
TradeFlowGuardian.sln                 ← solution file (root)
TradeFlowGuardian.Core/
TradeFlowGuardian.Infrastructure/
TradeFlowGuardian.Api/
TradeFlowGuardian.Worker/
TradeFlowGuardian.Tests/               ← xunit, targets net10.0
TradeFlowGuardian.Dashboard/           ← Vite 8 + React 19 + TypeScript
    package.json
    tsconfig.json
```

### Build commands

```bash
# .NET
dotnet build TradeFlowGuardian.sln --configuration Release

# .NET tests
dotnet test TradeFlowGuardian.Tests/TradeFlowGuardian.Tests.csproj \
  --configuration Release --no-build

# Dashboard
cd TradeFlowGuardian.Dashboard
npm ci
npm run build
```

### .NET SDK version

All projects target `net10.0`. Use `dotnet-version: '10.x'` in the workflow.

### Node version

The dashboard uses Vite 8, React 19, TypeScript ~6.0. Use `node-version: '22.x'`.

---

## Files to create

```
.github/workflows/ci.yml
```

(`.github/` directory may not exist yet — create it.)

---

## Acceptance criteria

- [ ] `.github/workflows/ci.yml` exists
- [ ] Workflow triggers on `pull_request` targeting `develop` and `main`, and on `push` to `develop` and `main`
- [ ] `dotnet build TradeFlowGuardian.sln --configuration Release` step present
- [ ] `dotnet test` step present, runs after build with `--no-build`
- [ ] `npm ci && npm run build` step present for the dashboard
- [ ] Dashboard build step runs from `TradeFlowGuardian.Dashboard/` working directory
- [ ] Workflow uses `actions/checkout@v4`
- [ ] .NET setup uses `actions/setup-dotnet@v4` with `dotnet-version: '10.x'`
- [ ] Node setup uses `actions/setup-node@v4` with `node-version: '22.x'`
- [ ] Jobs are named clearly (`build-and-test`, `build-dashboard`)
- [ ] Workflow file passes YAML lint (no syntax errors)
- [ ] No secrets or credentials in the workflow file
- [ ] `docs/DEPLOYMENT.md` updated to mention CI requirement before merging to `main`

---

## Out of scope

- Deploying to Railway from GitHub Actions (Railway handles this via branch push)
- Docker image builds in CI (Railway builds its own)
- Publishing NuGet packages
- Code coverage reporting (can be a follow-on)
- Dependabot or auto-merge setup

---

## Gotchas

- `dotnet test` will fail if Redis or Postgres are not available, BUT looking at
  `TradeFlowGuardian.Tests/` — the tests use Moq and do not hit real infrastructure.
  No service containers needed. Confirm by reading a few test files before assuming.
- `dotnet restore` is implicitly called by `dotnet build` — you don't need an explicit
  restore step, but it doesn't hurt.
- The dashboard `npm run build` calls `tsc -b && vite build`. TypeScript errors will
  fail the build, which is the desired gate.
- `npm ci` (not `npm install`) — CI should use the lock file.
- Make sure the `working-directory` is set correctly for the dashboard job steps —
  either use `working-directory: TradeFlowGuardian.Dashboard` on the job, or on each step.
- The solution file is at the repo root. The `dotnet` commands should run from the
  repo root (default checkout location), not from a subdirectory.
