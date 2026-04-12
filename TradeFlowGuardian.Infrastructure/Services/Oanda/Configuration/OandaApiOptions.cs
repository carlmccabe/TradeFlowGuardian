namespace TradeFlowGuardian.Infrastructure.Services.Oanda.Configuration;

public class OandaOptions
{
    public string ApiKey { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string ApiUrl { get; set; } = "https://api-fxpractice.oanda.com";
    public string StreamUrl { get; set; } = "https://stream-fxpractice.oanda.com";
}