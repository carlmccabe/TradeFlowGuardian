namespace TradeFlowGuardian.Infrastructure.Services.Oanda.Exceptions;

public class OandaConfigurationException : Exception
{
    public OandaConfigurationException(string message) : base(message)
    {
    }

    public OandaConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
