
using Newtonsoft.Json;

namespace TradeFlowGuardian.Infrastructure.Services.Oanda.Models.Requests;

public class OrderRequestModel
{
    [JsonProperty("order")] public Order Order { get; set; } = new();
}

public class Order
{
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;

    [JsonProperty("instrument")] public string Instrument { get; set; } = string.Empty;

    [JsonProperty("units")] public string Units { get; set; } = string.Empty;

    [JsonProperty("clientID")] public string? ClientID { get; set; }

    [JsonProperty("stopLossOnFill")] public StopLossDetails? StopLossOnFill { get; set; }

    [JsonProperty("takeProfitOnFill")] public TakeProfitDetails? TakeProfitOnFill { get; set; }
}

public class StopLossDetails
{
    [JsonProperty("price")] public string Price { get; set; } = string.Empty;

    [JsonProperty("timeInForce")] public string TimeInForce { get; set; } = "GTC"; // Good Till Cancelled
}

public class TakeProfitDetails
{
    [JsonProperty("price")] public string Price { get; set; } = string.Empty;

    [JsonProperty("timeInForce")] public string TimeInForce { get; set; } = "GTC"; // Good Till Cancelled
}