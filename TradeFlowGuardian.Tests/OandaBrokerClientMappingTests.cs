using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TradeFlowGuardian.Core.Enums;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using TradeFlowGuardian.Infrastructure.Brokers.Oanda;
using Xunit;

namespace TradeFlowGuardian.Tests;

/// <summary>
/// Pins the exact outgoing OANDA requests produced by the adapter so the broker
/// abstraction refactor stays byte-for-byte equivalent on the wire.
/// </summary>
public class OandaBrokerClientMappingTests
{
    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Captured { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Captured.Add((request, body));
            return responder(request);
        }
    }

    private static readonly ActiveOandaAccount Account =
        new("101-001-1234567-001", "test-api-key", "fxpractice", "test");

    private static OandaBrokerClient BuildClient(CapturingHandler handler)
    {
        var accounts = new Mock<IActiveAccountProvider>();
        accounts.Setup(a => a.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account);

        return new OandaBrokerClient(
            new HttpClient(handler),
            accounts.Object,
            NullLogger<OandaBrokerClient>.Instance);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
    };

    private static TradeSignal Signal(string instrument, SignalDirection direction) => new()
    {
        Instrument = instrument,
        Direction = direction,
        Price = 1.10000m,
        Atr = 0.001m,
        Timestamp = DateTimeOffset.UtcNow,
        IdempotencyKey = "test-key"
    };

    private const string FillResponse = """
        {"orderFillTransaction":{"id":"42","price":"1.10001","units":"1000"}}
        """;

    // ── market order mapping ──────────────────────────────────────────────────

    [Fact]
    public async Task PlaceMarketOrder_NonJpy_Sends5dpPrices_FOK_SignedUnits()
    {
        var handler = new CapturingHandler(_ => Json(FillResponse));
        var client = BuildClient(handler);

        var result = await client.PlaceMarketOrderAsync(
            Signal("EUR_USD", SignalDirection.Long),
            stopLoss: 1.09250m, takeProfit: 1.11500m, units: 1000);

        Assert.True(result.Success);
        var (request, body) = Assert.Single(handler.Captured);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://api-fxpractice.oanda.com/v3/accounts/101-001-1234567-001/orders",
            request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("test-api-key", request.Headers.Authorization.Parameter);

        var order = JsonNode.Parse(body!)!["order"]!;
        Assert.Equal("MARKET", order["type"]!.GetValue<string>());
        Assert.Equal("EUR_USD", order["instrument"]!.GetValue<string>());
        Assert.Equal("1000", order["units"]!.GetValue<string>());
        Assert.Equal("1.09250", order["stopLossOnFill"]!["price"]!.GetValue<string>());
        Assert.Equal("1.11500", order["takeProfitOnFill"]!["price"]!.GetValue<string>());
        Assert.Equal("FOK", order["timeInForce"]!.GetValue<string>());
    }

    [Fact]
    public async Task PlaceMarketOrder_Jpy_Sends3dpPrices()
    {
        var handler = new CapturingHandler(_ => Json(FillResponse));
        var client = BuildClient(handler);

        await client.PlaceMarketOrderAsync(
            Signal("USD_JPY", SignalDirection.Long),
            stopLoss: 148.7997m, takeProfit: 151.2003m, units: 500);

        var (_, body) = Assert.Single(handler.Captured);
        var order = JsonNode.Parse(body!)!["order"]!;
        Assert.Equal("148.800", order["stopLossOnFill"]!["price"]!.GetValue<string>());
        Assert.Equal("151.200", order["takeProfitOnFill"]!["price"]!.GetValue<string>());
    }

    [Fact]
    public async Task PlaceMarketOrder_Short_SendsNegativeUnitsString()
    {
        var handler = new CapturingHandler(_ => Json(FillResponse));
        var client = BuildClient(handler);

        await client.PlaceMarketOrderAsync(
            Signal("GBP_USD", SignalDirection.Short),
            stopLoss: 1.27000m, takeProfit: 1.25000m, units: 750);

        var (_, body) = Assert.Single(handler.Captured);
        Assert.Equal("-750", JsonNode.Parse(body!)!["order"]!["units"]!.GetValue<string>());
    }

    // ── close position mapping ────────────────────────────────────────────────

    [Fact]
    public async Task ClosePosition_LongOnly_SendsAllNone()
    {
        const string positionResponse = """
            {"position":{"long":{"units":"1000"},"short":{"units":"0"}}}
            """;
        const string closeResponse = """
            {"longOrderFillTransaction":{"id":"77","price":"1.10000"}}
            """;

        var handler = new CapturingHandler(req =>
            req.Method == HttpMethod.Get ? Json(positionResponse) : Json(closeResponse));
        var client = BuildClient(handler);

        var result = await client.ClosePositionAsync("EUR_USD");

        Assert.True(result.Success);
        Assert.Equal(2, handler.Captured.Count);

        var (getReq, _) = handler.Captured[0];
        Assert.Equal(
            "https://api-fxpractice.oanda.com/v3/accounts/101-001-1234567-001/positions/EUR_USD",
            getReq.RequestUri!.ToString());

        var (closeReq, closeBody) = handler.Captured[1];
        Assert.Equal(HttpMethod.Put, closeReq.Method);
        Assert.Equal(
            "https://api-fxpractice.oanda.com/v3/accounts/101-001-1234567-001/positions/EUR_USD/close",
            closeReq.RequestUri!.ToString());
        var node = JsonNode.Parse(closeBody!)!;
        Assert.Equal("ALL", node["longUnits"]!.GetValue<string>());
        Assert.Equal("NONE", node["shortUnits"]!.GetValue<string>());
    }

    [Fact]
    public async Task ClosePosition_NoOpenPosition_SendsNoCloseRequest()
    {
        const string positionResponse = """
            {"position":{"long":{"units":"0"},"short":{"units":"0"}}}
            """;

        var handler = new CapturingHandler(_ => Json(positionResponse));
        var client = BuildClient(handler);

        var result = await client.ClosePositionAsync("EUR_USD");

        Assert.True(result.Success);
        Assert.Single(handler.Captured); // position query only — no PUT
    }

    // ── descriptor ────────────────────────────────────────────────────────────

    [Fact]
    public void Descriptor_IsOandaAt30To1()
    {
        var client = BuildClient(new CapturingHandler(_ => Json("{}")));
        Assert.Equal("oanda", client.Descriptor.Name);
        Assert.Equal(30m, client.Descriptor.Leverage);
    }
}
