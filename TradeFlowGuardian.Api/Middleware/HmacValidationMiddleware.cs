using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TradeFlowGuardian.Core.Configuration;

namespace TradeFlowGuardian.Api.Middleware;

/// <summary>
/// Validates HMAC-SHA256 signature on incoming webhook requests.
///
/// TradingView webhook setup:
/// 1. Set a secret string in TV alert → Webhook URL → add header X-Signature
/// 2. Set the same secret in WebhookConfig:Secret (env var or user secrets)
///
/// </summary>

public class HmacValidationMiddleware(
    RequestDelegate next,
    IOptions<WebhookConfig> config,
    ILogger<HmacValidationMiddleware> logger)
{
    private readonly WebhookConfig _config = config.Value;

    private const string SignatureHeader = "X-Signature";
    private const string WebhookPath = "/api/signal";

    public async Task InvokeAsync(HttpContext context)
    {
        // Only validate the signal endpoint
        if (!context.Request.Path.StartsWithSegments(WebhookPath) 
            || context.Request.Method != HttpMethods.Post)
        {
            await next(context);
            return;
        }

        // Buffer body so we can read it for signature + pass it downstream
        context.Request.EnableBuffering();
        var body = await new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (!context.Request.Headers.TryGetValue(SignatureHeader, out var signatureHeader))
        {
            logger.LogWarning("Webhook request missing {Header}", SignatureHeader);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Missing signature header");
            return;
        }

        var signature = signatureHeader.ToString();
        if (!ValidateSignature(body, signature))
        {
            logger.LogWarning("Webhook HMAC validation failed — possible tampered payload");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid signature");
            return;
        }

        await next(context);
    }

    private bool ValidateSignature(string body, string signature)
    {
        // Strip "sha256=" prefix if present
        var raw = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signature[7..]
            : signature;

        var keyBytes = Encoding.UTF8.GetBytes(_config.Secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        using var hmac = new HMACSHA256(keyBytes);
        var computed = hmac.ComputeHash(bodyBytes);
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(raw.ToLowerInvariant()));
    }
}
