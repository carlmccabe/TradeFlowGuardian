using Prometheus;

namespace TradeFlowGuardian.Infrastructure.Observability;

/// <summary>
/// Central registry for all custom Prometheus metrics.
/// Static class uses prometheus-net's DefaultRegistry — the same registry
/// that KestrelMetricServer and UseHttpMetrics() read from automatically.
///
/// Label design:
///   - Low-cardinality labels only (fixed set of values)
///   - No instrument labels on counters — too many pairs = cardinality explosion
///   - Filter reason uses short snake_case labels, not free-form strings
/// </summary>
public static class TradeMetrics
{
    // ── Counters ──────────────────────────────────────────────────────────────

    /// <summary>Signals that passed idempotency check and entered processing.</summary>
    public static readonly Counter SignalsReceived = Metrics
        .CreateCounter(
            "tradeflow_signals_received_total",
            "Total trade signals that entered processing (post idempotency check)");

    /// <summary>Signals blocked by any filter. Label: filter name in snake_case.</summary>
    public static readonly Counter SignalsFiltered = Metrics
        .CreateCounter(
            "tradeflow_signals_filtered_total",
            "Total signals blocked by filters",
            new CounterConfiguration
            {
                LabelNames = ["reason"]  // "atr_spike" | "signal_too_old" | "news_window"
            });

    /// <summary>Orders submitted to OANDA. Label: "success" | "failed".</summary>
    public static readonly Counter OrdersPlaced = Metrics
        .CreateCounter(
            "tradeflow_orders_placed_total",
            "Total orders submitted to OANDA",
            new CounterConfiguration
            {
                LabelNames = ["outcome"]  // "success" | "failed"
            });

    // ── Histogram ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Latency from PlaceMarketOrderAsync call start to OANDA response.
    /// Buckets tuned for FX execution: 100ms (fast) to 10s (slow/timeout).
    /// </summary>
    public static readonly Histogram OrderLatencySeconds = Metrics
        .CreateHistogram(
            "tradeflow_order_latency_seconds",
            "OANDA order placement latency in seconds",
            new HistogramConfiguration
            {
                Buckets = [0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0]
            });

    // ── Gauges ────────────────────────────────────────────────────────────────

    /// <summary>Current OANDA account NAV. Updated on every signal processed.</summary>
    public static readonly Gauge AccountBalance = Metrics
        .CreateGauge(
            "tradeflow_account_balance",
            "Current OANDA account NAV balance in account currency (AUD)");

    /// <summary>
    /// Pending messages in the Redis Stream consumer group.
    /// Measures queue lag — should stay near 0 under normal operation.
    /// Uses XPENDING count, not XLEN, to reflect actual backlog.
    /// </summary>
    public static readonly Gauge RedisQueueDepth = Metrics
        .CreateGauge(
            "tradeflow_redis_queue_depth",
            "Pending (unacknowledged) messages in the Redis Stream consumer group");
}
