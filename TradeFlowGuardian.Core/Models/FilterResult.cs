namespace TradeFlowGuardian.Core.Models;

public class FilterResult
{
    public bool Allowed { get; init; }
    public string Reason { get; init; } = string.Empty;

    public static FilterResult Allow() => new() { Allowed = true, Reason = "OK" };
    public static FilterResult Block(string reason) => new() { Allowed = false, Reason = reason };
}
