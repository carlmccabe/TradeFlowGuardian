using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using TradeFlowGuardian.Infrastructure.Accounts;

namespace TradeFlowGuardian.Api.Controllers;

/// <summary>
/// OANDA account registry management. All endpoints require the admin secret
/// (X-Admin-Secret header, same value as the webhook secret) — these mutate
/// live trading credentials and must never be open like the status endpoints.
/// API keys are write-only: accepted on create, never returned.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AccountsController(
    IServiceProvider services,
    IActiveAccountProvider activeProvider,
    IConnectionMultiplexer redis,
    IOptions<WebhookConfig> webhook,
    ILogger<AccountsController> logger) : ControllerBase
{
    private const string SecretHeader = "X-Admin-Secret";

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (NotAuthorized(out var challenge)) return challenge;
        if (StoreUnavailable(out var store, out var unavailable)) return unavailable;

        var accounts = await store.GetAllAsync(ct);
        return Ok(accounts.Select(Project));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest body, CancellationToken ct)
    {
        if (NotAuthorized(out var challenge)) return challenge;
        if (StoreUnavailable(out var store, out var unavailable)) return unavailable;

        if (string.IsNullOrWhiteSpace(body.Label) ||
            string.IsNullOrWhiteSpace(body.AccountId) ||
            string.IsNullOrWhiteSpace(body.ApiKey))
            return BadRequest(new { error = "label, accountId and apiKey are required" });

        if (body.Environment is not ("fxpractice" or "fxtrade"))
            return BadRequest(new { error = "environment must be 'fxpractice' or 'fxtrade'" });

        if (body.Activate && body.Environment == "fxtrade" && !body.ConfirmLive)
            return BadRequest(new { error = "activating a live (fxtrade) account requires confirmLive=true" });

        try
        {
            var account = await store.CreateAsync(
                body.Label, body.AccountId, body.Environment, body.ApiKey, body.Activate, ct);

            if (body.Activate)
                await NotifyAccountChangedAsync(account, ct);

            return CreatedAtAction(nameof(GetAll), Project(account));
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return Conflict(new { error = "an account with this accountId and environment already exists" });
        }
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, [FromBody] ActivateAccountRequest? body, CancellationToken ct)
    {
        if (NotAuthorized(out var challenge)) return challenge;
        if (StoreUnavailable(out var store, out var unavailable)) return unavailable;

        var target = (await store.GetAllAsync(ct)).FirstOrDefault(a => a.Id == id);
        if (target is null) return NotFound();

        if (target.Environment == "fxtrade" && body?.ConfirmLive != true)
            return BadRequest(new { error = "activating a live (fxtrade) account requires confirmLive=true" });

        var account = await store.ActivateAsync(id, ct);
        if (account is null) return NotFound();

        await NotifyAccountChangedAsync(account, ct);
        return Ok(Project(account));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (NotAuthorized(out var challenge)) return challenge;
        if (StoreUnavailable(out var store, out var unavailable)) return unavailable;

        try
        {
            var deleted = await store.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>The account the system is currently trading on — safe fields only.</summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        try
        {
            var active = await activeProvider.GetActiveAsync(ct);
            return Ok(new
            {
                label = active.Label,
                accountId = active.AccountId,
                environment = active.Environment,
                isLive = active.IsLive
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private bool NotAuthorized(out IActionResult challenge)
    {
        var expected = webhook.Value.Secret;
        var provided = Request.Headers[SecretHeader].FirstOrDefault();
        if (string.IsNullOrEmpty(expected) || provided != expected)
        {
            logger.LogWarning("Rejected account management request — missing or invalid {Header}", SecretHeader);
            challenge = StatusCode(StatusCodes.Status401Unauthorized, new { error = "invalid or missing X-Admin-Secret header" });
            return true;
        }
        challenge = Ok();
        return false;
    }

    private bool StoreUnavailable(out IOandaAccountStore store, out IActionResult unavailable)
    {
        var resolved = services.GetService<IOandaAccountStore>();
        if (resolved is null)
        {
            store = null!;
            unavailable = StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "account registry requires Postgres — Postgres:ConnectionString is not configured" });
            return true;
        }
        store = resolved;
        unavailable = Ok();
        return false;
    }

    private async Task NotifyAccountChangedAsync(OandaAccount account, CancellationToken ct)
    {
        activeProvider.Invalidate();

        var sub = redis.GetSubscriber();
        // Cache invalidation for every Api/Worker instance.
        await sub.PublishAsync(RedisChannel.Literal(ActiveAccountProvider.ChangedChannel), "changed");
        // Dashboard push via the existing events → SignalR bridge.
        var payload = JsonSerializer.Serialize(new
        {
            type = "account_changed",
            label = account.Label,
            accountId = account.AccountId,
            environment = account.Environment
        });
        await sub.PublishAsync(RedisChannel.Literal("tradeflow:events"), payload);

        logger.LogWarning("Active account is now {Label} ({Env} {AccountId})",
            account.Label, account.Environment, account.AccountId);
    }

    private static object Project(OandaAccount a) => new
    {
        id = a.Id,
        label = a.Label,
        accountId = a.AccountId,
        environment = a.Environment,
        isActive = a.IsActive,
        createdAt = a.CreatedAt,
        updatedAt = a.UpdatedAt
    };
}

public record CreateAccountRequest(
    string Label,
    string AccountId,
    string Environment,
    string ApiKey,
    bool Activate = false,
    bool ConfirmLive = false);

public record ActivateAccountRequest(bool ConfirmLive = false);
