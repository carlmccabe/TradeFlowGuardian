using Newtonsoft.Json;

namespace TradeFlowGuardian.Infrastructure.Services.Oanda.Models.Requests;

public class ClosePositionRequest
{
    [JsonProperty("longUnits")]
    public string LongUnits { get; set; } = "ALL";

    [JsonProperty("shortUnits")]
    public string ShortUnits { get; set; } = "ALL";
}
