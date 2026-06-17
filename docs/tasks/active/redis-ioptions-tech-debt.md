# Task: Redis IOptions Tech Debt

**Branch:** `develop`
**BACKLOG status to set:** `active` when you start, `done` when merged
**Scope:** Two files, two lines each — surgical fix only

---

## What to fix

Both `TradeFlowGuardian.Api/Program.cs` and `TradeFlowGuardian.Worker/Program.cs`
read the Redis connection string directly from `IConfiguration`:

```csharp
// Current (violates CLAUDE.md convention)
var cs = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
```

This bypasses the `IOptions<RedisConfig>` pattern used everywhere else in the codebase.
The `RedisConfig` class is already defined and already registered via
`builder.Services.Configure<RedisConfig>(builder.Configuration.GetSection("Redis"))`.

The fix reads the connection string through `RedisConfig` instead.

---

## Context

Read `CLAUDE.md` and `docs/tasks/AGENT_INSTRUCTIONS.md` first.

CLAUDE.md convention: "Config via IOptions<T> pattern — never read IConfiguration
directly in services." The TECH_DEBT.md item is:
> `Program.cs:26` reads `Redis:ConnectionString` via `IConfiguration` directly —
> should use `IOptions<RedisConfig>` binding.

### Why the current pattern is wrong

The `IConnectionMultiplexer` factory lambda currently ignores the already-registered
`RedisConfig` and reads the raw config key. This means:
- The connection string is sourced from a different binding path than all other Redis code
- If `RedisConfig.ConnectionString` had any transformation logic added later, it would
  be bypassed here

### The fix

In the `AddSingleton<IConnectionMultiplexer>` factory lambda, the `sp` parameter
is an `IServiceProvider`. Use it to resolve `IOptions<RedisConfig>`:

```csharp
// Fixed
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConfig = sp.GetRequiredService<IOptions<RedisConfig>>().Value;
    var cs = string.IsNullOrWhiteSpace(redisConfig.ConnectionString)
        ? "localhost:6379"
        : redisConfig.ConnectionString;
    return ConnectionMultiplexer.Connect(ParseRedisOptions(cs));
});
```

Note: `builder.Services.Configure<RedisConfig>(...)` is called **before** the
`AddSingleton<IConnectionMultiplexer>` registration in both files, so the options
are registered and available when the factory lambda runs (at first resolution, not
at registration time).

---

## Files to change

| File | Line | Change |
|---|---|---|
| `TradeFlowGuardian.Api/Program.cs` | ~60 | Replace `builder.Configuration["Redis:ConnectionString"]` with `IOptions<RedisConfig>` factory pattern |
| `TradeFlowGuardian.Worker/Program.cs` | ~57 | Same change |

Confirm the exact line numbers by searching for `builder.Configuration["Redis:ConnectionString"]`
in both files before editing.

---

## Files to read before starting

| File | Why |
|---|---|
| `TradeFlowGuardian.Api/Program.cs` | Contains the code to fix (~line 60) |
| `TradeFlowGuardian.Worker/Program.cs` | Same fix needed (~line 57) |
| `TradeFlowGuardian.Core/Configuration/AppConfig.cs` | `RedisConfig` class definition — confirm property name is `ConnectionString` |
| `docs/TECH_DEBT.md` | Mark item resolved when done |

---

## Acceptance criteria

- [ ] `builder.Configuration["Redis:ConnectionString"]` no longer appears in `Api/Program.cs`
- [ ] `builder.Configuration["Redis:ConnectionString"]` no longer appears in `Worker/Program.cs`
- [ ] Both files use `sp.GetRequiredService<IOptions<RedisConfig>>().Value.ConnectionString`
- [ ] The `using Microsoft.Extensions.Options;` using directive is present in both files (it probably already is — check)
- [ ] Fallback to `"localhost:6379"` is preserved when `ConnectionString` is null or empty
- [ ] `dotnet build TradeFlowGuardian.sln` — 0 errors
- [ ] All existing tests pass (`dotnet test`)
- [ ] `docs/TECH_DEBT.md` item for `Program.cs` `IConfiguration` direct read is marked `[x]` resolved with today's date

---

## Out of scope

- Any other `IConfiguration` direct reads elsewhere in the codebase
- Changing `RedisConfig` or any other config class
- Adding new tests (the existing tests don't test DI wiring; this is covered by the build)
- Changing any Redis-related behaviour

---

## Gotchas

- The `IOptions<T>` registration (`builder.Services.Configure<RedisConfig>(...)`) must
  be called **before** the `AddSingleton<IConnectionMultiplexer>(sp => ...)` registration
  for the factory to resolve `IOptions<RedisConfig>` successfully. Verify the order hasn't
  changed in the file since this brief was written.
- The factory lambda is lazy — it runs on first `IConnectionMultiplexer` resolution,
  not at startup. By that point all DI registrations are complete, so order within
  `builder.Services` doesn't technically matter. But keep the configure call first
  anyway for readability.
- After the fix, the startup config banner in both `Program.cs` files logs
  `redisCfg.ConnectionString` (from `IOptions<RedisConfig>`) for the host display.
  That banner already uses `IOptions<RedisConfig>` correctly — this fix makes the
  `IConnectionMultiplexer` registration consistent with it.
