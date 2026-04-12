using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;
using TradeFlowGuardian.Infrastructure.Services.Oanda.Configuration;

namespace TradeFlowGuardian.Infrastructure.Services.Oanda;

public class OandaHttpClient
{
    private readonly RestClient client;
    private readonly string apiKey;
    private readonly ILogger<OandaHttpClient> logger;

    public string AccountId { get; }

    public OandaHttpClient(RestClient client, IOptions<OandaOptions> options, ILogger<OandaHttpClient> logger)
    {
        this.client = client;
        this.logger = logger;

        var cfg = options.Value;
        apiKey = cfg.ApiKey ?? throw new ArgumentNullException(nameof(cfg.ApiKey), "OANDA ApiKey missing");
        AccountId = cfg.AccountId ?? throw new ArgumentNullException(nameof(cfg.AccountId), "OANDA AccountId missing");
    }

    public async Task<RestResponse> ExecuteGetRequestAsync(string endpoint, string operation, params (string key, string value)[] parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("🔍 {Operation}...", operation);
        
        try
        {
            var request = new RestRequest(endpoint);
            request.AddHeader("Authorization", $"Bearer {apiKey}");
            
            foreach (var (key, value) in parameters)
            {
                request.AddParameter(key, value);
            }

            var response = await client.ExecuteAsync(request);
            stopwatch.Stop();

            LogResponse(operation, response, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "💥 Error during {Operation} after {ElapsedMs}ms", operation, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<RestResponse> ExecutePostRequestAsync(string endpoint, object requestBody, string operation)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("📤 {Operation}...", operation);
        
        try
        {
            var request = new RestRequest(endpoint, Method.Post);
            request.AddHeader("Authorization", $"Bearer {apiKey}");
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(requestBody);

            var response = await client.ExecuteAsync(request);
            stopwatch.Stop();

            LogResponse(operation, response, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "💥 Error during {Operation} after {ElapsedMs}ms", operation, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public void ValidateResponse(RestResponse response, string operation)
    {
        if (!response.IsSuccessful || response.Content == null)
        {
            var errorMsg = $"Failed {operation}: {response.StatusCode}";
            if (!string.IsNullOrEmpty(response.Content))
            {
                errorMsg += $" - {response.Content}";
            }
            throw new InvalidOperationException(errorMsg);
        }
    }

    private void LogResponse(string operation, RestResponse response, long elapsedMs)
    {
        if (response.IsSuccessful)
        {
            logger.LogInformation("✅ {Operation} completed successfully in {ElapsedMs}ms", operation, elapsedMs);
        }
        else
        {
            logger.LogError("❌ {Operation} failed: {StatusCode} - {Content} (took {ElapsedMs}ms)",
                operation, response.StatusCode, response.Content, elapsedMs);
        }
    }
}
