# Database Migrations

Schema changes are numbered SQL files in [`docs/migrations/`](./migrations/), applied
automatically by a Railway **pre-deploy command** on the Api service. The runner
(`SqlMigrationRunner`) is hand-rolled on Npgsql/Dapper — no EF migrations, no DbUp.

## How it works

- Every `docs/migrations/NNN_name.sql` file is embedded into the Api assembly at build time.
- `dotnet TradeFlowGuardian.Api.dll --migrate-only` applies pending migrations and exits
  (0 = success, 1 = failure → Railway aborts the deploy). It never starts Kestrel.
- Applied versions are tracked in a `schema_versions` table
  (`version, name, applied_at, checksum`), created automatically on first run.
- A Postgres **advisory lock** guards the whole run, so two instances racing can't
  double-apply.
- Each migration runs in **its own transaction**; on failure it rolls back, the runner
  stops, and later migrations are not attempted.
- Normal app startup **never** migrates — it only logs a warning if pending migrations
  are detected.

## Adding a migration

1. Create `docs/migrations/NNN_short_name.sql` — next number, zero-padded to 3 digits
   (e.g. `005_add_trade_tags.sql`). Numbers must be unique.
2. Write plain Postgres SQL. Prefer idempotent statements (`IF NOT EXISTS`) — the runner
   guarantees single application, but idempotent SQL makes manual recovery easier.
3. Test locally: `docker compose up -d postgres`, then from the Api build output run
   `dotnet TradeFlowGuardian.Api.dll --migrate-only` with
   `Postgres__ConnectionString` pointing at your local DB.
4. Merge to `main` → staging pre-deploy applies it → verify → promote to `production`.

## Immutability rule

**A migration file is immutable once merged.** The runner stores a SHA-256 checksum at
apply time and hard-fails the deploy if an already-applied file's content no longer
matches (or if the file disappears from the build). To change something, add a new
migration — never edit an old one.

## Baseline — adopting an existing database

`--migrate-baseline N` records versions 001..N in `schema_versions` **without executing
them**, for databases where those migrations were already applied by hand. It refuses to
run if `schema_versions` already has rows, and requires the explicit version argument.

## Adoption plan (one-time, manual)

1. Merge the runner to `main` → staging deploys. Nothing runs yet (no pre-deploy
   command set).
2. **Verify drift first**: confirm 003's `risk_settings` seed rows and 004's
   `oanda_accounts` schema are actually present in both staging and prod. If either DB
   drifted from the SQL files, fix it by hand BEFORE baselining.
3. Run the baseline once per environment (Railway shell or one-off command on the Api
   service):
   ```
   dotnet TradeFlowGuardian.Api.dll --migrate-baseline 4
   ```
   First against the staging DB, then against prod. Verify:
   `SELECT * FROM schema_versions ORDER BY version;` → 4 rows.
4. Set the Railway **pre-deploy command** on the Api service in both environments:
   ```
   dotnet TradeFlowGuardian.Api.dll --migrate-only
   ```
5. The next real migration (005) ships automatically — verify it in staging before
   promoting to production.

## Testing

Unit tests run everywhere. The integration tests (apply/idempotency/tamper/baseline/
advisory-lock) need a real Postgres and are skipped unless `TFG_TEST_POSTGRES` is set:

```sh
docker compose up -d postgres
TFG_TEST_POSTGRES="Host=localhost;Username=tradeflow;Password=tradeflow;Database=tradeflow" \
  dotnet test TradeFlowGuardian.Tests/TradeFlowGuardian.Tests.csproj
```

Each test creates and drops its own scratch database, so the connection string's user
needs `CREATEDB` rights.
