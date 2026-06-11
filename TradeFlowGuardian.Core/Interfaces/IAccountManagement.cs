using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Core.Interfaces;

/// <summary>
/// Encrypts/decrypts secrets stored at rest (OANDA API keys).
/// Backed by ASP.NET Data Protection with keys shared between Api and Worker.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

/// <summary>
/// CRUD over the oanda_accounts registry. Scoped (EF-backed).
/// </summary>
public interface IOandaAccountStore
{
    Task<IReadOnlyList<OandaAccount>> GetAllAsync(CancellationToken ct = default);

    Task<OandaAccount> CreateAsync(
        string label, string accountId, string environment, string apiKey,
        bool activate, CancellationToken ct = default);

    /// <summary>Deactivates all other accounts and activates the given one. Null if not found.</summary>
    Task<OandaAccount?> ActivateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Deletes an account. The active account cannot be deleted.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Decrypted credentials for the active account, or null if none is active.</summary>
    Task<ActiveOandaAccount?> GetActiveAsync(CancellationToken ct = default);
}

/// <summary>
/// Resolves the active account for OANDA calls. Singleton — caches briefly and
/// invalidates on the Redis account-changed channel so Api and Worker switch together.
/// </summary>
public interface IActiveAccountProvider
{
    /// <summary>Throws InvalidOperationException when no account is configured anywhere.</summary>
    Task<ActiveOandaAccount> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Drops the cached account so the next call re-reads the store.</summary>
    void Invalidate();
}
