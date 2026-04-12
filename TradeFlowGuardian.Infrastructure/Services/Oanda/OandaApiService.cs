using TradeFlowGuardian.Domain.Entities;
using TradeFlowGuardian.Infrastructure.Services.Oanda.Exceptions;
using TradeFlowGuardian.Infrastructure.Services.Oanda.Models.Requests;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TradeFlowGuardian.Infrastructure.Services.Oanda;

public class OandaApiService : IOandaApiService
{
    private readonly OandaHttpClient httpClient;
    private readonly ILogger<OandaApiService> _logger;

    public OandaApiService(OandaHttpClient httpClient, ILogger<OandaApiService> logger)
    {
        this.httpClient = httpClient;
        _logger = logger;

        _logger.LogInformation("🔗 OANDA API Service initialized. AccountId={AccountId}", httpClient.AccountId);
    }

    public async Task<bool> ValidateConnectionAsync()
    {
        try
        {
            var response = await httpClient.ExecuteGetRequestAsync(
                $"/v3/accounts/{httpClient.AccountId}",
                "Validating OANDA API connection"
            );

            return response.IsSuccessful;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error validating OANDA API connection");
            return false;
        }
    }

    public async Task<decimal> GetCurrentPriceAsync(string instrument)
    {
        try
        {
            var response = await httpClient.ExecuteGetRequestAsync(
                $"/v3/accounts/{httpClient.AccountId}/pricing",
                $"Getting current price for {instrument}",
                ("instruments", instrument)
            );

            httpClient.ValidateResponse(response, $"get price for {instrument}");

            // todo - handle multiple prices
            dynamic? data = JsonConvert.DeserializeObject(response.Content!);
            var price = (decimal)data?.prices[0]?.closeoutAsk;

            _logger.LogInformation("💰 Current price for {Instrument}: {Price}", instrument, price);
            return price;
        }
        catch (Exception ex) when (!(ex is OandaApiException))
        {
            _logger.LogError(ex, "💥 Error getting current price for {Instrument}", instrument);
            throw new OandaApiException($"Failed to get current price for {instrument}", ex);
        }
    }

    public async Task<string> PlaceMarketOrderAsync(string instrument, int units, string side)
    {
        try
        {
            _logger.LogWarning("⚡ PLACING LIVE ORDER: {Side} {Units} units of {Instrument}",
                side.ToUpper(), Math.Abs(units), instrument);

            var orderRequest = new
            {
                order = new
                {
                    type = "MARKET",
                    instrument,
                    units = side.ToUpper() == "BUY" ? Math.Abs(units).ToString() : (-Math.Abs(units)).ToString()
                }
            };

            _logger.LogInformation("📤 Order request payload: {OrderRequest}",
                JsonConvert.SerializeObject(orderRequest));

            var response = await httpClient.ExecutePostRequestAsync(
                $"/v3/accounts/{httpClient.AccountId}/orders",
                orderRequest,
                "Placing market order"
            );

            httpClient.ValidateResponse(response, "place order");

            dynamic? data = JsonConvert.DeserializeObject(response.Content!);
            var orderId = data?.orderCreateTransaction?.id ?? "Unknown";

            _logger.LogWarning($"✅ ORDER EXECUTED: ID={orderId}, {side.ToUpper()} {Math.Abs(units)} {instrument}");

            return orderId;
        }
        catch (Exception ex) when (!(ex is OandaApiException))
        {
            _logger.LogError(ex, "💥 CRITICAL: Order placement failed for {Instrument}", instrument);
            throw new OandaApiException($"Failed to place order for {instrument}", ex);
        }
    }

    public async Task<IEnumerable<Position>> GetPositionsAsync()
    {
        try
        {
            var response = await httpClient.ExecuteGetRequestAsync(
                $"/v3/accounts/{httpClient.AccountId}/positions",
                "Retrieving current positions"
            );

            httpClient.ValidateResponse(response, "get positions");

            dynamic? data = JsonConvert.DeserializeObject(response.Content!);
            var positions = new List<Position>();

            if (data?.positions != null)
            {
                foreach (var positionData in data.positions)
                {
                    // Parse long and short sides
                    var longUnits = (decimal)(positionData.@long?.units ?? 0);
                    var shortUnits = (decimal)(positionData.@short?.units ?? 0);
                    var longAvgPrice = (decimal)(positionData.@long?.averagePrice ?? 0);
                    var shortAvgPrice = (decimal)(positionData.@short?.averagePrice ?? 0);
                    var longUnrealizedPL = (decimal)(positionData.@long?.unrealizedPL ?? 0);
                    var shortUnrealizedPL = (decimal)(positionData.@short?.unrealizedPL ?? 0);

                    // Only include positions that have actual units (open positions)
                    if (longUnits == 0 && shortUnits == 0) continue;
                    var netUnits = longUnits + shortUnits; // Net position
                    var avgPrice = netUnits > 0 ? longAvgPrice : (netUnits < 0 ? shortAvgPrice : 0m);
                    var unrealizedPL = longUnrealizedPL + shortUnrealizedPL;

                    var position = new Position
                    {
                        Instrument = (string)positionData.instrument,
                        Units = (long)netUnits,
                        AveragePrice = avgPrice,
                        UnrealizedPL = unrealizedPL,
                        Side = netUnits > 0 ? "long" : (netUnits < 0 ? "short" : "flat"),

                        // Additional details for debugging/logging
                        LongUnits = (long)longUnits,
                        ShortUnits = (long)shortUnits,
                        LongAveragePrice = longAvgPrice,
                        ShortAveragePrice = shortAvgPrice
                    };

                    positions.Add(position);

                    _logger.LogInformation(
                        "📍 Open Position: {Instrument} - {Units} units @ {AvgPrice} (P&L: {PnL:F2})",
                        position.Instrument, position.Units, position.AveragePrice, position.UnrealizedPL);
                }
            }

            _logger.LogInformation("📈 Retrieved {PositionCount} positions", positions.Count);
            return positions;
        }
        catch (Exception ex) when (!(ex is OandaApiException))
        {
            _logger.LogError(ex, "💥 Error getting positions");
            throw new OandaApiException("Failed to get positions", ex);
        }
    }

    public async Task<AccountSummary> GetAccountSummaryAsync()
    {
        try
        {
            var response = await httpClient.ExecuteGetRequestAsync(
                $"/v3/accounts/{httpClient.AccountId}/summary",
                "Retrieving account summary"
            );

            httpClient.ValidateResponse(response, "get account summary");

            dynamic? data = JsonConvert.DeserializeObject(response.Content!);

            var summary = new AccountSummary
            {
                AccountId = httpClient.AccountId,
                Balance = data?.account?.balance ?? 0m,
                UnrealizedPL = data?.account?.unrealizedPL ?? 0m,
                RealizedPL = data?.account?.pl ?? 0m,
                MarginUsed = data?.account?.marginUsed ?? 0m,
                MarginAvailable = data?.account?.marginAvailable ?? 0m,
                MarginRate = data?. account.marginRate ?? 0m
            };

            _logger.LogInformation(
                "💰 Account Summary - Balance: ${Balance:F2}, P&L: ${PnL:F2}, Margin Used: ${MarginUsed:F2}",
                summary.Balance, summary.UnrealizedPL + summary.RealizedPL, summary.MarginUsed);

            return summary;
        }
        catch (Exception ex) when (!(ex is OandaApiException))
        {
            _logger.LogError(ex, "💥 Error getting account summary");
            throw new OandaApiException("Failed to get account summary", ex);
        }
    }

    public async Task<IEnumerable<Candle>> GetCandlesAsync(string instrument, string granularity,
        bool includeIncomplete, int count)
    {
        try
        {
            // Build parameters - only include includeFirst if we have a from parameter
            var parameters = new List<(string key, string value)>
            {
                ("granularity", granularity),
                ("count", count.ToString())
            };

            // Note: includeFirst/includeIncomplete only applies when using 'from' parameter
            // When using 'count' without 'from', OANDA automatically handles incomplete candles
            // So we'll ignore the includeIncomplete parameter for now when using count-based requests

            var response = await httpClient.ExecuteGetRequestAsync(
                $"/v3/instruments/{instrument}/candles",
                $"Getting {count} {granularity} candles for {instrument}",
                parameters.ToArray()
            );

            httpClient.ValidateResponse(response, $"get candles for {instrument}");

            dynamic? data = JsonConvert.DeserializeObject(response.Content!);
            var candles = new List<Candle>();

            if (data?.candles != null)
            {
                foreach (var candleData in data.candles)
                {
                    // When using count-based requests, OANDA typically returns complete candles
                    // We can still filter based on the complete flag if needed
                    if (candleData != null)
                    {
                        var isComplete = candleData.complete == true;

                        // Include the candle if:
                        // 1. It's complete, OR
                        // 2. We explicitly want incomplete candles AND it's the most recent one
                        if (isComplete || includeIncomplete)
                        {
                            candles.Add(new Candle
                            {
                                Instrument = instrument,
                                Granularity = granularity,
                                Time = DateTime.Parse((string)candleData.time), // Use Time instead of Timestamp
                                Open = (decimal)candleData.mid.o,
                                High = (decimal)candleData.mid.h,
                                Low = (decimal)candleData.mid.l,
                                Close = (decimal)candleData.mid.c,
                                Volume = (int)candleData.volume
                            });
                        }
                    }
                }
            }

            _logger.LogInformation("📊 Retrieved {CandleCount} {Granularity} candles for {Instrument}",
                candles.Count, granularity, instrument);
            return candles;
        }
        catch (Exception ex) when (!(ex is OandaApiException))
        {
            _logger.LogError(ex, "💥 Error getting candles for {Instrument}", instrument);
            throw new OandaApiException($"Failed to get candles for {instrument}", ex);
        }
    }

    public async Task<IEnumerable<Candle>> GetCandlesAsync(
        string instrument,
        string granularity,
        DateTime fromUtc,
        DateTime toUtc,
        bool includeIncomplete = false)
    {
        try
        {
            // Build query strictly by time window
            var parameters = new List<(string key, string value)>
            {
                ("granularity", granularity),
                ("from", fromUtc.ToUniversalTime().ToString("o")),
                ("to", toUtc.ToUniversalTime().ToString("o")),
                // price=M uses midpoint prices; adjust if you need bid/ask
                ("price", "M"),
                // includeFirst=true includes a candle that starts exactly at 'from' if aligned
                ("includeFirst", "true")
            };

            var response = await httpClient.ExecuteGetRequestAsync(
                $"/v3/instruments/{instrument}/candles",
                $"Getting {granularity} candles for {instrument} from {fromUtc:O} to {toUtc:O}",
                parameters.ToArray()
            );

            httpClient.ValidateResponse(response, $"get candles for {instrument}");

            dynamic? data = JsonConvert.DeserializeObject(response.Content!);
            var candles = new List<Candle>();

            if (data?.candles != null)
            {
                foreach (var candleData in data.candles)
                {
                    if (candleData == null) continue;

                    var isComplete = candleData.complete == true;
                    if (isComplete || includeIncomplete)
                    {
                        candles.Add(new Candle
                        {
                            Instrument = instrument,
                            Granularity = granularity,
                            Time = DateTime.Parse((string)candleData.time),
                            Open = (decimal)candleData.mid.o,
                            High = (decimal)candleData.mid.h,
                            Low = (decimal)candleData.mid.l,
                            Close = (decimal)candleData.mid.c,
                            Volume = (int)candleData.volume
                        });
                    }
                }
            }

            _logger.LogInformation("📊 Retrieved {CandleCount} {Granularity} candles for {Instrument} (window)",
                candles.Count, granularity, instrument);
            return candles;
        }
        catch (Exception ex) when (ex is not OandaApiException)
        {
            _logger.LogError(ex, "💥 Error getting candles (window) for {Instrument}", instrument);
            throw new OandaApiException($"Failed to get candles for {instrument}", ex);
        }
    }
    
    // todo: implement model for pip info
    public async Task<(int PipLocation, decimal PipSize, decimal PipValuePerUnit)> GetInstrumentPipInfoAsync(
        string instrument)
    {
        try
        {
            // Use query parameter (avoid duplicating ?instruments= in path)
            var response = await httpClient.ExecuteGetRequestAsync(
                $"/v3/accounts/{httpClient.AccountId}/instruments",
                $"Getting instrument info for {instrument}",
                ("instruments", instrument)
            );

            httpClient.ValidateResponse(response, $"get instrument info for {instrument}");

            dynamic? data = JsonConvert.DeserializeObject(response.Content!);
            var instrumentData = data?.instruments?[0];

            if (instrumentData == null)
                throw new OandaApiException($"No instrument data found for {instrument}");

            var pipLocation = (int)instrumentData.pipLocation;
            var pipSize = (decimal)Math.Pow(10, pipLocation);

            // Convert pip value per unit into AUD (account currency)
            var pipValuePerUnitAud = await CalculatePipValuePerUnitAudAsync(instrument, pipSize);

            _logger.LogInformation(
                "📏 Pip info for {Instrument} (AUD): Location={PipLocation}, Size={PipSize}, ValuePerUnit(AUD)={PipValuePerUnit}",
                instrument, pipLocation, pipSize, pipValuePerUnitAud);

            return (pipLocation, pipSize, pipValuePerUnitAud);
        }
        catch (Exception ex) when (!(ex is OandaApiException))
        {
            _logger.LogError(ex, "💥 Error getting pip info for {Instrument}", instrument);
            throw new OandaApiException($"Failed to get pip info for {instrument}", ex);
        }
    }

    // Pip value conversion helpers (AUD account)
    private async Task<decimal> CalculatePipValuePerUnitAudAsync(string instrument, decimal pipSize)
    {
        // pip value per unit is always "pipSize" in quote currency units;
        // convert quote currency -> AUD
        var parts = instrument.Split('_');
        if (parts.Length != 2) return pipSize; // fallback

        var quote = parts[1];

        var quoteToAud = await GetQuoteToAudRateAsync(quote);
        return pipSize * quoteToAud;
    }

    // Returns how many AUD for 1 unit of the quote currency
    private async Task<decimal> GetQuoteToAudRateAsync(string quote)
    {
        if (quote == "AUD") return 1m;

        // Prefer AUD_QUOTE (returns QUOTE per 1 AUD), invert to get AUD per 1 QUOTE
        var audQuote = $"AUD_{quote}";
        var quoteAud = $"{quote}_AUD";

        try
        {
            var priceAudQuote = await GetCurrentPriceAsync(audQuote); // QUOTE per AUD
            if (priceAudQuote > 0)
                return 1m / priceAudQuote; // AUD per QUOTE
        }
        catch
        {
            // ignore, try the inverse
        }

        try
        {
            var priceQuoteAud = await GetCurrentPriceAsync(quoteAud); // AUD per QUOTE
            if (priceQuoteAud > 0)
                return priceQuoteAud;
        }
        catch
        {
            // ignore, fallback below
        }

        // Conservative fallback if pair not available
        _logger.LogWarning("⚠️ Could not resolve conversion for {Quote}->AUD, using fallback 0.67", quote);
        return 0.67m;
    }

    public async Task<bool> HasOpenPositionAsync(string instrument)
    {
        try
        {
            var response = await httpClient.ExecuteGetRequestAsync(
                $"/v3/accounts/{httpClient.AccountId}/positions/{instrument}",
                $"Checking position for {instrument}"
            );

            // OANDA returns 404 if no position exists, which is normal
            if (response is { IsSuccessful: false, StatusCode: System.Net.HttpStatusCode.NotFound })
            {
                _logger.LogInformation("📍 No position found for {Instrument}", instrument);
                return false;
            }

            httpClient.ValidateResponse(response, $"check position for {instrument}");

            dynamic? data = JsonConvert.DeserializeObject(response.Content!);
            var position = data?.position;

            if (position == null) return false;

            var longUnits = (decimal)(position.@long?.units ?? 0);
            var shortUnits = (decimal)(position.@short?.units ?? 0);
            var hasPosition = longUnits != 0 || shortUnits != 0;

            _logger.LogInformation(
                "🎯 Position check for {Instrument}: {HasPosition} (Long: {LongUnits}, Short: {ShortUnits})",
                instrument, hasPosition, longUnits, shortUnits);

            return hasPosition;
        }
        catch (Exception ex) when (ex is not OandaApiException)
        {
            _logger.LogError(ex, "💥 Error checking position for {Instrument}", instrument);
            throw new OandaApiException($"Failed to check position for {instrument}", ex);
        }
    }

    public async Task<(long Units, decimal AveragePrice)> GetOpenPositionAsync(string instrument)
    {
        try
        {
            var response = await httpClient.ExecuteGetRequestAsync(
                $"/v3/accounts/{httpClient.AccountId}/positions/{instrument}",
                $"Getting position details for {instrument}"
            );

            httpClient.ValidateResponse(response, $"get position for {instrument}");

            dynamic? data = JsonConvert.DeserializeObject(response.Content!);
            var position = data?.position;

            if (position == null)
                return (0, 0m);

            var longUnits = (decimal)(position.@long?.units ?? 0);
            var shortUnits = (decimal)(position.@short?.units ?? 0);
            var longAvgPrice = (decimal)(position.@long?.averagePrice ?? 0);
            var shortAvgPrice = (decimal)(position.@short?.averagePrice ?? 0);

            // Return net position (long units are positive, short units are negative in OANDA)
            var netUnits = longUnits + shortUnits;
            var avgPrice = netUnits > 0 ? longAvgPrice : (netUnits < 0 ? shortAvgPrice : 0m);

            _logger.LogInformation("📊 Position for {Instrument}: {Units} units @ {AvgPrice}",
                instrument, netUnits, avgPrice);

            return ((long)netUnits, avgPrice);
        }
        catch (Exception ex) when (!(ex is OandaApiException))
        {
            _logger.LogError(ex, "💥 Error getting position for {Instrument}", instrument);
            throw new OandaApiException($"Failed to get position for {instrument}", ex);
        }
    }

    public async Task<string> PlaceMarketOrderAsync(string instrument, long units, decimal? stopLoss,
        decimal? takeProfit, string clientTag)
    {
        try
        {
            _logger.LogWarning("⚡ PLACING LIVE ORDER: {Units} units of {Instrument} (SL: {StopLoss}, TP: {TakeProfit})",
                units, instrument, stopLoss, takeProfit);

            // Using proper DTO instead of anonymous objects
            var orderRequest = new OrderRequestModel
            {
                Order = new Order
                {
                    Type = "MARKET",
                    Instrument = instrument,
                    Units = units.ToString(),
                    ClientID = clientTag,
                    StopLossOnFill = stopLoss.HasValue
                        ? new StopLossDetails { Price = stopLoss.Value.ToString("F5") }
                        : null,
                    TakeProfitOnFill = takeProfit.HasValue
                        ? new TakeProfitDetails { Price = takeProfit.Value.ToString("F5") }
                        : null
                }
            };

            _logger.LogInformation("📤 Order request: {OrderRequest}", JsonConvert.SerializeObject(orderRequest));

            var response = await httpClient.ExecutePostRequestAsync(
                $"/v3/accounts/{httpClient.AccountId}/orders",
                orderRequest,
                "Placing market order with stops"
            );

            httpClient.ValidateResponse(response, "place market order");

            dynamic? data = JsonConvert.DeserializeObject(response.Content!);
            var orderId = data?.orderCreateTransaction?.id ?? "Unknown";

            _logger.LogWarning($"✅ ORDER EXECUTED: ID={orderId}, {units} {instrument}");
            return orderId;
        }
        catch (Exception ex) when (!(ex is OandaApiException))
        {
            _logger.LogError(ex, "💥 CRITICAL: Order placement failed for {Instrument}", instrument);
            throw new OandaApiException($"Failed to place order for {instrument}", ex);
        }
    }

    public async Task ClosePositionAsync(string instrument)
    {
        try
        {
            _logger.LogWarning("🔒 CLOSING POSITION: {Instrument}", instrument);

            // OANDA requires separate calls to close long and short positions
            var closeRequest = new ClosePositionRequest
            {
                LongUnits = "ALL",
                ShortUnits = "ALL"
            };

            var response = await httpClient.ExecutePostRequestAsync(
                $"/v3/accounts/{httpClient.AccountId}/positions/{instrument}/close",
                closeRequest,
                $"Closing position for {instrument}"
            );

            httpClient.ValidateResponse(response, $"close position for {instrument}");

            dynamic? data = JsonConvert.DeserializeObject(response.Content!);
            var longOrderId = data?.longOrderCreateTransaction?.id;
            var shortOrderId = data?.shortOrderCreateTransaction?.id;

            var closedOrders = new List<string>();
            if (longOrderId != null) closedOrders.Add($"Long: {longOrderId}");
            if (shortOrderId != null) closedOrders.Add($"Short: {shortOrderId}");

            _logger.LogWarning("✅ POSITION CLOSED: {Instrument} - Orders: [{Orders}]",
                instrument, string.Join(", ", closedOrders));
        }
        catch (Exception ex) when (!(ex is OandaApiException))
        {
            _logger.LogError(ex, "💥 CRITICAL: Failed to close position for {Instrument}", instrument);
            throw new OandaApiException($"Failed to close position for {instrument}", ex);
        }
    }


    /// <summary>
    /// Place a market order but wait for confirmation (poll order state) so callers can know if the trade actually opened.
    /// Adds a clientTag for idempotency and performs a lightweight pre-flight pricing/spread check.
    /// </summary>
    public async Task<(string OrderId, string Status)> PlaceMarketOrderWithConfirmationAsync(
        string instrument,
        long units,
        decimal? stopLoss,
        decimal? takeProfit,
        string clientTag,
        decimal maxSpreadPips = 2m,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        var start = DateTime.UtcNow;

        // 1) Pre-flight: get pricing and ensure spread acceptable
        try
        {
            var pricingResp = await httpClient.ExecuteGetRequestAsync(
                $"/v3/accounts/{httpClient.AccountId}/pricing?instruments={instrument}",
                "Get pricing before placing market order"
            );

            httpClient.ValidateResponse(pricingResp, "get pricing");

            var pricingJson = JObject.Parse(pricingResp.Content!);
            var prices = pricingJson["prices"]?[0];
            if (prices == null)
            {
                _logger.LogWarning("Pricing unavailable for {Instrument}, aborting market order.", instrument);
                return (OrderId: "none", Status: "pricing_unavailable");
            }

            decimal bid = prices["bids"]?[0]?["price"]?.Value<decimal>() ?? decimal.Zero;
            decimal ask = prices["asks"]?[0]?["price"]?.Value<decimal>() ?? decimal.Zero;

            decimal pipSize = instrument.EndsWith("_JPY", StringComparison.OrdinalIgnoreCase) ? 0.01m : 0.0001m;
            decimal spreadPips = Math.Round((ask - bid) / pipSize, 2);

            _logger.LogInformation("Pre-flight pricing {Instrument} bid={Bid} ask={Ask} spreadPips={SpreadPips}",
                instrument, bid, ask, spreadPips);

            if (spreadPips > maxSpreadPips)
            {
                _logger.LogWarning("Spread too wide ({SpreadPips} > {MaxSpread}) aborting order for {Instrument}",
                    spreadPips, maxSpreadPips, instrument);
                return (OrderId: "none", Status: "spread_too_wide");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pre-flight pricing check failed, proceeding cautiously.");
            // proceed but log warning — caller decides whether to accept
        }

        // 2) Place market order (reuse existing internal DTO path)
        string placedOrderId;
        try
        {
            var orderRequest = new OrderRequestModel
            {
                Order = new Order
                {
                    Type = "MARKET",
                    Instrument = instrument,
                    Units = units.ToString(),
                    ClientID = clientTag,
                    StopLossOnFill = stopLoss.HasValue
                        ? new StopLossDetails { Price = stopLoss.Value.ToString("F5") }
                        : null,
                    TakeProfitOnFill = takeProfit.HasValue
                        ? new TakeProfitDetails { Price = takeProfit.Value.ToString("F5") }
                        : null
                }
            };

            _logger.LogWarning("Placing MARKET order: {Side} {Units} {Instrument} tag={Tag}",
                units > 0 ? "BUY" : "SELL", Math.Abs(units), instrument, clientTag);
            var response = await httpClient.ExecutePostRequestAsync(
                $"/v3/accounts/{httpClient.AccountId}/orders",
                orderRequest,
                "Placing market order"
            );

            httpClient.ValidateResponse(response, "place market order");
            var data = JsonConvert.DeserializeObject<JObject>(response.Content!);

            // OANDA returns orderCreateTransaction.id (transaction id), and orderFillTransaction may be present for immediate fills
            placedOrderId = data?["orderCreateTransaction"]?["id"]?.Value<string>()
                            ?? data?["orderFillTransaction"]?["orderID"]?.Value<string>()
                            ?? (data?["orderCreateTransaction"]?["clientOrderID"]?.Value<string>() ?? "unknown");

            _logger.LogInformation("Order request accepted. OrderTransactionId={OrderTx}", placedOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to place market order for {Instrument}", instrument);
            return (OrderId: "none", Status: "place_failed");
        }

        // 3) Poll order / transactions until we confirm filled or failed
        try
        {
            string lastStatus = "submitted";
            while (DateTime.UtcNow - start < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Query the order endpoint first
                var orderResp = await httpClient.ExecuteGetRequestAsync(
                    $"/v3/accounts/{httpClient.AccountId}/orders/{placedOrderId}",
                    "Get order status");

                // If the order endpoint returns 404 (not found) it might be a transaction-only id; fall back to transactions
                if (orderResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var txResp = await httpClient.ExecuteGetRequestAsync(
                        $"/v3/accounts/{httpClient.AccountId}/transactions/{placedOrderId}",
                        "Get transaction status"
                    );
                    if (txResp.IsSuccessStatusCode)
                    {
                        var txJson = JObject.Parse(txResp.Content!);
                        lastStatus = txJson["transaction"]?["type"]?.Value<string>() ?? lastStatus;
                        if (string.Equals(lastStatus, "ORDER_FILL", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(lastStatus, "MARKET_ORDER_FILL", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(lastStatus, "ORDER_FILL_TRANSACTION", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Order {OrderId} filled (transaction type {TxType})", placedOrderId,
                                lastStatus);
                            return (OrderId: placedOrderId, Status: "filled");
                        }
                    }
                }
                else if (orderResp.IsSuccessStatusCode)
                {
                    var orderJson = JObject.Parse(orderResp.Content!);
                    var state = orderJson["order"]?["state"]?.Value<string>() ??
                                lastStatus; // e.g. "FILLED", "CANCELLED", "PENDING"
                    lastStatus = state;
                    if (string.Equals(state, "FILLED", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Order {OrderId} state=FILLED", placedOrderId);
                        return (OrderId: placedOrderId, Status: "filled");
                    }

                    if (string.Equals(state, "CANCELLED", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(state, "REJECTED", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Order {OrderId} state={State}", placedOrderId, state);
                        return (OrderId: placedOrderId, Status: state.ToLowerInvariant());
                    }
                }

                await Task.Delay(400, cancellationToken);
            }

            _logger.LogWarning("Timeout waiting for order confirmation for {OrderId}", placedOrderId);
            return (OrderId: placedOrderId, Status: "timeout");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Order confirmation polling cancelled for {OrderId}", placedOrderId);
            return (OrderId: placedOrderId, Status: "cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while polling order confirmation for {OrderId}", placedOrderId);
            return (OrderId: placedOrderId, Status: "poll_error");
        }
    }
}
