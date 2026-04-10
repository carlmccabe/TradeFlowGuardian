namespace TradeFlowGuardian.Core.Configuration;

public class OandaConfig
{
    public required string ApiKey { get; init; }
    public required string AccountId { get; init; }

    /// <summary>"fxpractice" for paper, "fxtrade" for live</summary>
    public string Environment { get; init; } = "fxpractice";

    public string BaseUrl => Environment == "fxtrade"
        ? "https://api-fxtrade.oanda.com"
        : "https://api-fxpractice.oanda.com";

    public string StreamUrl => Environment == "fxtrade"
        ? "https://stream-fxtrade.oanda.com"
        : "https://stream-fxpractice.oanda.com";
}

public class RiskConfig
{
    public decimal DefaultRiskPercent { get; init; } = 1.0m;
    public decimal MaxPositionUnits { get; init; } = 1_000_000m;
    public decimal AtrStopMultiplier { get; init; } = 2.0m;
    public decimal AtrTargetMultiplier { get; init; } = 4.0m;
    public decimal MaxDailyDrawdownPercent { get; init; } = 3.0m;
}

public class FilterConfig
{
    public bool EnableAtrSpikeFilter { get; init; } = true;

    /// <summary>Block if current ATR > rolling average × this multiplier</summary>
    public decimal AtrSpikeMultiplier { get; init; } = 2.0m;

    public bool EnableNewsFilter { get; init; } = true;

    /// <summary>Minutes before/after high-impact news to block execution</summary>
    public int NewsBufferMinutes { get; init; } = 30;

    public bool EnableSessionFilter { get; init; } = false;

    /// <summary>Max age of incoming signal before rejection (seconds)</summary>
    public int SignalMaxAgeSeconds { get; init; } = 60;
}

public class WebhookConfig
{
    /// <summary>HMAC-SHA256 secret — must match TradingView alert webhook secret</summary>
    public required string Secret { get; init; }
}
