using TradeFlowGuardian.Core.Models;

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

public class NewsFilterOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Minutes before a high-impact event to block new entries.</summary>
    public int BlockWindowMinutesBefore { get; set; } = 30;

    /// <summary>Minutes after a high-impact event to block new entries.</summary>
    public int BlockWindowMinutesAfter { get; set; } = 30;

    /// <summary>Events below this level are ignored.</summary>
    public ImpactLevel MinimumImpactLevel { get; set; } = ImpactLevel.High;

    /// <summary>How often to refresh the ForexFactory iCal feed (hours).</summary>
    public int CacheRefreshHours { get; set; } = 6;
}

public class RedisConfig
{
    public string ConnectionString { get; init; } = "localhost:6379";

    /// <summary>Redis Stream key — shared between API (writer) and Worker (reader)</summary>
    public string StreamName { get; init; } = "tradeflow:signals";

    /// <summary>Consumer group name — all Worker replicas share this group</summary>
    public string ConsumerGroup { get; init; } = "workers";

    /// <summary>Unique per Worker instance — use hostname in production</summary>
    public string ConsumerName { get; init; } = "worker-1";
}

public class PostgresConfig
{
    /// <summary>
    /// Postgres connection string.
    /// Supports either:
    /// - Npgsql format: "Host=...;Database=tradeflow;Username=...;Password=..."
    /// - Railway format: "postgresql://user:password@host:port/database"
    /// Leave empty to disable trade history persistence (not recommended for production).
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}
