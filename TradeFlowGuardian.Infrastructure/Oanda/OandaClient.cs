using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Enums;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Oanda;

/// <summary>
/// OANDA v20 REST API client.
/// Docs: https://developer.oanda.com/rest-live-v20/introduction/
/// </summary>
public class OandaClient : IOandaClient
{
    private readonly HttpClient _http;
    private readonly OandaConfig _config;
    private readonly ILogger<OandaClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public OandaClient(HttpClient http, IOptions<OandaConfig> config, ILogger<OandaClient> logger)
    {
        _http = http;
        _config = config.Value;
        _logger = logger;

        _http.BaseAddress = new Uri(_config.BaseUrl);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Place a market order with attached SL/TP.
    /// Units positive = buy (long), negative = sell (short).
    /// </summary>
    public async Task<TradeResult> PlaceMarketOrderAsync(
        TradeSignal signal,
        decimal stopLoss,
        decimal takeProfit,
        long units,
        CancellationToken ct = default)
    {
        var signedUnits = signal.Direction == SignalDirection.Short ? -Math.Abs(units) : Math.Abs(units);

        // OANDA price format: 5 decimal places for non-JPY, 3 for JPY
        var isJpy = signal.Instrument.Contains("JPY");
        var priceFmt = isJpy ? "F3" : "F5";

        var body = new
        {
            order = new
            {
                type = "MARKET",
                instrument = signal.Instrument,
                units = signedUnits.ToString(),
                stopLossOnFill = new { price = stopLoss.ToString(priceFmt) },
                takeProfitOnFill = new { price = takeProfit.ToString(priceFmt) },
                timeInForce = "FOK"  // Fill-or-kill — no partial fills
            }
        };

        var json = JsonSerializer.Serialize(body, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"/v3/accounts/{_config.AccountId}/orders";
        _logger.LogInformation("Placing {Direction} order: {Instrument} {Units} units | SL={SL} TP={TP}",
            signal.Direction, signal.Instrument, signedUnits, stopLoss.ToString(priceFmt), takeProfit.ToString(priceFmt));

        try
        {
            var response = await _http.PostAsync(url, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OANDA order failed {Status}: {Body}", response.StatusCode, responseBody);
                return TradeResult.Failed($"OANDA {response.StatusCode}: {responseBody}");
            }

            var node = JsonNode.Parse(responseBody);
            var fillNode = node?["orderFillTransaction"];

            if (fillNode is null)
            {
                // Order may have been cancelled (insufficient margin, FOK miss, etc.)
                var cancelNode = node?["orderCancelTransaction"];
                var reason = cancelNode?["reason"]?.ToString() ?? "Unknown — no fill transaction";
                _logger.LogWarning("Order not filled: {Reason}", reason);
                return TradeResult.Failed($"Order not filled: {reason}");
            }

            var orderId = fillNode["id"]?.ToString() ?? "unknown";
            var fillPrice = decimal.Parse(fillNode["price"]?.ToString() ?? "0");
            var filledUnits = long.Parse(fillNode["units"]?.ToString() ?? "0");

            _logger.LogInformation("Order filled: ID={OrderId} Price={Price} Units={Units}",
                orderId, fillPrice, filledUnits);

            return TradeResult.Succeeded(orderId, fillPrice, Math.Abs(filledUnits), stopLoss, takeProfit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception placing OANDA order");
            return TradeResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Close all open units for an instrument.
    /// </summary>
    public async Task<TradeResult> ClosePositionAsync(string instrument, CancellationToken ct = default)
    {
        var url = $"/v3/accounts/{_config.AccountId}/positions/{instrument}/close";
        var body = new { longUnits = "ALL", shortUnits = "ALL" };
        var content = new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");

        _logger.LogInformation("Closing position: {Instrument}", instrument);

        try
        {
            var response = await _http.PutAsync(url, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Close position failed {Status}: {Body}", response.StatusCode, responseBody);
                return TradeResult.Failed($"Close failed {response.StatusCode}: {responseBody}");
            }

            return new TradeResult
            {
                Success = true,
                OrderId = "close",
                Message = "Position closed",
                FillPrice = 0,
                Units = 0,
                StopLoss = 0,
                TakeProfit = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception closing position {Instrument}", instrument);
            return TradeResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns current NAV balance in account currency (AUD).
    /// </summary>
    public async Task<decimal> GetAccountBalanceAsync(CancellationToken ct = default)
    {
        var url = $"/v3/accounts/{_config.AccountId}/summary";

        try
        {
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(body);
            var balance = node?["account"]?["NAV"]?.ToString() ?? "0";
            return decimal.Parse(balance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch account balance");
            return 0m;
        }
    }

    /// <summary>
    /// Returns the mid price (bid+ask)/2 for an instrument. Null on failure.
    /// Uses the non-streaming pricing snapshot endpoint.
    /// GET /v3/accounts/{id}/pricing?instruments={instrument}
    /// </summary>
    public async Task<decimal?> GetMidPriceAsync(string instrument, CancellationToken ct = default)
    {
        var url = $"/v3/accounts/{_config.AccountId}/pricing?instruments={instrument}";

        try
        {
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(body);
            var price = node?["prices"]?[0];

            if (price is null)
                return null;

            var bid = decimal.Parse(price["bids"]?[0]?["price"]?.ToString() ?? "0");
            var ask = decimal.Parse(price["asks"]?[0]?["price"]?.ToString() ?? "0");

            if (bid <= 0 || ask <= 0)
                return null;

            return (bid + ask) / 2m;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch mid price for {Instrument}", instrument);
            return null;
        }
    }

    /// <summary>
    /// Returns a full price snapshot for an instrument, including Bid, Ask, Mid, and Spread.
    /// GET /v3/accounts/{id}/pricing?instruments={instrument}
    /// </summary>
    public async Task<PriceSnapshot?> GetPriceSnapshotAsync(string instrument, CancellationToken ct = default)
    {
        var url = $"/v3/accounts/{_config.AccountId}/pricing?instruments={instrument}";

        try
        {
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(body);
            var price = node?["prices"]?[0];

            if (price is null)
                return null;

            var bid = decimal.Parse(price["bids"]?[0]?["price"]?.ToString() ?? "0");
            var ask = decimal.Parse(price["asks"]?[0]?["price"]?.ToString() ?? "0");

            if (bid <= 0 || ask <= 0)
                return null;

            return new PriceSnapshot(
                instrument,
                bid,
                ask,
                (bid + ask) / 2m,
                ask - bid,
                DateTimeOffset.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch price snapshot for {Instrument}", instrument);
            return null;
        }
    }

    /// <summary>
    /// Returns net open units for an instrument. Null if no position.
    /// Positive = long, negative = short.
    /// </summary>
    public async Task<decimal?> GetOpenPositionUnitsAsync(string instrument, CancellationToken ct = default)
    {
        var url = $"/v3/accounts/{_config.AccountId}/positions/{instrument}";

        try
        {
            var response = await _http.GetAsync(url, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(body);

            var longUnits = decimal.Parse(node?["position"]?["long"]?["units"]?.ToString() ?? "0");
            var shortUnits = decimal.Parse(node?["position"]?["short"]?["units"]?.ToString() ?? "0");

            var net = longUnits + shortUnits;
            return net == 0 ? null : net;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch position for {Instrument}", instrument);
            return null;
        }
    }
}
