using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TradeFlowGuardian.Core.Configuration;

namespace TradeFlowGuardian.Api.Middleware;

/// <summary>
/// Validates incoming webhook requests by comparing the ?secret= query parameter
/// against the configured WebhookConfig.Secret.
///
/// TradingView webhook setup:
/// 1. Append ?secret=YOUR_SECRET to the webhook URL in the TV alert
/// 2. Set the same secret in WebhookConfig:Secret (env var or user secrets)
///
/// HTTPS ensures the secret is not transmitted in plaintext.
/// Constant-time comparison prevents timing attacks.
/// </summary>

public class HmacValidationMiddleware(
    RequestDelegate next,
    IOptions<WebhookConfig> config,
    ILogger<HmacValidationMiddleware> logger)
{
    private readonly WebhookConfig _config = config.Value;

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

        // Buffer body so it can be read downstream
        context.Request.EnableBuffering();
        var body = await new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (!context.Request.Query.TryGetValue("secret", out var secret))
        {
            logger.LogWarning("Webhook request missing secret query parameter");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Missing secret query parameter");
            return;
        }

        var secretBytes = Encoding.UTF8.GetBytes(secret.ToString());
        var configBytes = Encoding.UTF8.GetBytes(_config.Secret);

        if (!CryptographicOperations.FixedTimeEquals(secretBytes, configBytes))
        {
            logger.LogWarning("Webhook secret validation failed — invalid or mismatched secret");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid secret");
            return;
        }

        await next(context);
    }
}
