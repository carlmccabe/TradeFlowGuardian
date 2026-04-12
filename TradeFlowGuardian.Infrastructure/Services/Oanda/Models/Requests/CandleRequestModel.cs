namespace TradeFlowGuardian.Infrastructure.Services.Oanda.Models.Requests;

public class CandleRequest
{
    public string Instrument { get; set; } = string.Empty;
    public string Granularity { get; set; } = "M5";
    public int Count { get; set; } = 500;
    public bool IncludeFirst { get; set; } = false;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
