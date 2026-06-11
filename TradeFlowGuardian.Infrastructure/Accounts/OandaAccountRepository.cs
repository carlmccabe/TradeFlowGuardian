using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using TradeFlowGuardian.Infrastructure.Data;

namespace TradeFlowGuardian.Infrastructure.Accounts;

public class OandaAccountRepository(
    TradeFlowDbContext db,
    ISecretProtector protector,
    ILogger<OandaAccountRepository> logger) : IOandaAccountStore
{
    public async Task<IReadOnlyList<OandaAccount>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.OandaAccounts
            .AsNoTracking()
            .OrderByDescending(a => a.IsActive)
            .ThenBy(a => a.Label)
            .ToListAsync(ct);
    }

    public async Task<OandaAccount> CreateAsync(
        string label, string accountId, string environment, string apiKey,
        bool activate, CancellationToken ct = default)
    {
        var account = new OandaAccount
        {
            Id = Guid.NewGuid(),
            Label = label.Trim(),
            AccountId = accountId.Trim(),
            Environment = environment,
            ApiKeyEncrypted = protector.Protect(apiKey.Trim()),
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.OandaAccounts.Add(account);

        if (activate)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await DeactivateAllAsync(ct);
            account.IsActive = true;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        else
        {
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("OANDA account registered: {Label} ({Env} {AccountId}) active={Active}",
            account.Label, account.Environment, account.AccountId, account.IsActive);
        return account;
    }

    public async Task<OandaAccount?> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var account = await db.OandaAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null) return null;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await DeactivateAllAsync(ct);
        account.IsActive = true;
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogWarning("Active OANDA account switched to {Label} ({Env} {AccountId})",
            account.Label, account.Environment, account.AccountId);
        return account;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var account = await db.OandaAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null) return false;
        if (account.IsActive)
            throw new InvalidOperationException("Cannot delete the active account — activate another account first.");

        db.OandaAccounts.Remove(account);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("OANDA account deleted: {Label} ({Env} {AccountId})",
            account.Label, account.Environment, account.AccountId);
        return true;
    }

    public async Task<ActiveOandaAccount?> GetActiveAsync(CancellationToken ct = default)
    {
        var account = await db.OandaAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.IsActive, ct);
        if (account is null) return null;

        return new ActiveOandaAccount(
            account.AccountId,
            protector.Unprotect(account.ApiKeyEncrypted),
            account.Environment,
            account.Label);
    }

    private Task<int> DeactivateAllAsync(CancellationToken ct) =>
        db.OandaAccounts
            .Where(a => a.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.IsActive, false)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow), ct);
}
