namespace TradeFlowGuardian.Core.Models;

/// <summary>
/// A registered OANDA account (row in oanda_accounts). The API key is stored
/// encrypted at rest — use IOandaAccountStore / IActiveAccountProvider to get
/// decrypted credentials; this entity never exposes the plaintext key.
/// </summary>
public class OandaAccount
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;

    /// <summary>"fxpractice" for paper, "fxtrade" for live.</summary>
    public string Environment { get; set; } = "fxpractice";

    public string ApiKeyEncrypted { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Decrypted credentials for the currently active account — resolved per call
/// by OandaClient so an account switch takes effect without a restart.
/// </summary>
public sealed record ActiveOandaAccount(
    string AccountId,
    string ApiKey,
    string Environment,
    string Label)
{
    public bool IsLive => Environment == "fxtrade";

    public string BaseUrl => IsLive
        ? "https://api-fxtrade.oanda.com"
        : "https://api-fxpractice.oanda.com";

    public string StreamUrl => IsLive
        ? "https://stream-fxtrade.oanda.com"
        : "https://stream-fxpractice.oanda.com";
}
