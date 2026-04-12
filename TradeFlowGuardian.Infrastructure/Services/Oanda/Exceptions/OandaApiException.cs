using System.Net;

namespace TradeFlowGuardian.Infrastructure.Services.Oanda.Exceptions;

public class OandaApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public string? ResponseContent { get; }

    public OandaApiException(string message) : base(message)
    {
    }

    public OandaApiException(string message, HttpStatusCode statusCode, string? responseContent = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }

    public OandaApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}