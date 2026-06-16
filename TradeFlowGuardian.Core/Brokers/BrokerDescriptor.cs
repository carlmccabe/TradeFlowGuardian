namespace TradeFlowGuardian.Core.Brokers;

/// <summary>
/// Static capabilities of a broker adapter.
/// </summary>
/// <param name="Name">Broker identifier, e.g. "oanda". Matches the account registry's broker discriminator.</param>
/// <param name="Leverage">
/// Effective retail leverage used for margin-cap position sizing (e.g. 30 for 30:1).
/// This is the regulatory leverage the engine sizes against, which may be lower than
/// what the broker's API reports.
/// </param>
public record BrokerDescriptor(string Name, decimal Leverage);
