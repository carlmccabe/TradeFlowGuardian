namespace TradeFlowGuardian.Core.Models;

public class FilterResult
{
    public bool Allowed { get; init; }
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Short snake_case label for Prometheus metrics — avoids high-cardinality
    /// label explosion from embedding variable values (ATR figures, seconds) in
    /// the reason string. E.g. "atr_spike", "signal_too_old", "ok".
    /// </summary>
    public string Label { get; init; } = "ok";

    public static FilterResult Allow() =>
        new() { Allowed = true, Reason = "OK", Label = "ok" };

    public static FilterResult Block(string reason, string label) =>
        new() { Allowed = false, Reason = reason, Label = label };
}
