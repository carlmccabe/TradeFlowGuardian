using Microsoft.AspNetCore.DataProtection;
using TradeFlowGuardian.Core.Interfaces;

namespace TradeFlowGuardian.Infrastructure.Accounts;

/// <summary>
/// ASP.NET Data Protection wrapper. Keys are persisted to Redis with a shared
/// application name so ciphertext written by the Api is readable by the Worker.
/// </summary>
public class DataProtectionSecretProtector : ISecretProtector
{
    // Changing this purpose string invalidates every stored API key — never change it.
    public const string Purpose = "TradeFlowGuardian.OandaApiKeys.v1";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);
    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
