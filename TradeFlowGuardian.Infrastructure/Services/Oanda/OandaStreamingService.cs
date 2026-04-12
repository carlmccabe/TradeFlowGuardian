using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TradeFlowGuardian.Infrastructure.Services.Oanda.StreamingModels;

namespace TradeFlowGuardian.Infrastructure.Services.Oanda;

// Long-lived lightweight client for the OANDA transactions stream (practice URL hard-coded).
public class OandaStreamingService : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseStreamUrl;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public event Action<TransactionEvent>? TransactionReceived;
    public event Action<Exception>? StreamError;
    public event Action? StreamClosed;

    // New: real-time pricing ticks
    public event Action<PriceTick>? PriceReceived;

    // Optional: heartbeat notifications from pricing stream
    public event Action<DateTime>? PricingHeartbeat;

    public OandaStreamingService(string baseStreamUrl = "https://stream-fxpractice.oanda.com")
    {
        _baseStreamUrl = baseStreamUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseStreamUrl));
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    public void StartTransactionsStream(string accessToken, string accountId,
        CancellationToken? externalToken = null)
    {
        if (_listenTask != null && !_listenTask.IsCompleted) return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken ?? CancellationToken.None);
        _listenTask = Task.Run(() => ListenTransactionsAsync(accessToken, accountId, _cts.Token),
            CancellationToken.None);
    }

    // New: start pricing stream for one or more instruments (comma-separated list)
    public Task StartPricingStreamAsync(string accessToken, string accountId, string instrumentsCsv,
        CancellationToken? externalToken = null)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(externalToken ?? CancellationToken.None);
        return ListenPricingAsync(accessToken, accountId, instrumentsCsv, linked.Token);
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
        }

        _listenTask = null;
    }

    public void Dispose()
    {
        Stop();
        _http.Dispose();
    }

    private async Task ListenTransactionsAsync(string accessToken, string accountId, CancellationToken ct)
    {
        var url = $"{_baseStreamUrl}/v3/accounts/{accountId}/transactions/stream";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("Accept", "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            StreamError?.Invoke(ex);
            return;
        }

        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            while (!ct.IsCancellationRequested && !reader.EndOfStream)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var evt = TransactionEvent.FromJson(root);
                    if (evt != null)
                    {
                        TransactionReceived?.Invoke(evt);
                    }
                }
                catch (JsonException)
                {
                    // ignore partial/invalid lines
                }
                catch (Exception ex)
                {
                    StreamError?.Invoke(ex);
                }
            }
        }
        finally
        {
            StreamClosed?.Invoke();
        }
    }

    // New: pricing stream listener
    private async Task ListenPricingAsync(string accessToken, string accountId, string instrumentsCsv,
        CancellationToken ct)
    {
        var url =
            $"{_baseStreamUrl}/v3/accounts/{accountId}/pricing/stream?instruments={Uri.EscapeDataString(instrumentsCsv)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("Accept", "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            StreamError?.Invoke(ex);
            return;
        }

        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            while (!ct.IsCancellationRequested && !reader.EndOfStream)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("type", out var tProp)) continue;
                    var type = tProp.GetString();

                    if (string.Equals(type, "HEARTBEAT", StringComparison.OrdinalIgnoreCase))
                    {
                        if (root.TryGetProperty("time", out var timeProp) &&
                            DateTime.TryParse(timeProp.GetString(), out var ts))
                        {
                            PricingHeartbeat?.Invoke(ts);
                        }

                        continue;
                    }

                    if (!string.Equals(type, "PRICE", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var instrument = root.GetProperty("instrument").GetString()!;
                    var timeStr = root.GetProperty("time").GetString()!;
                    var closeoutBid = root.TryGetProperty("closeoutBid", out var cb) ? cb.GetDecimal() : 0m;
                    var closeoutAsk = root.TryGetProperty("closeoutAsk", out var ca) ? ca.GetDecimal() : 0m;

                    var tick = new PriceTick
                    {
                        Instrument = instrument,
                        Time = DateTime.Parse(timeStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
                        CloseoutBid = closeoutBid,
                        CloseoutAsk = closeoutAsk,
                        Mid = closeoutBid > 0m && closeoutAsk > 0m ? (closeoutBid + closeoutAsk) / 2m : 0m
                    };

                    PriceReceived?.Invoke(tick);
                }
                catch (JsonException)
                {
                    // ignore partial/invalid lines
                }
                catch (Exception ex)
                {
                    StreamError?.Invoke(ex);
                }
            }
        }
        finally
        {
            // Pricing stream closed - notify if desired (reuse StreamClosed or separate event)
            StreamClosed?.Invoke();
        }
    }
}

// New: simple tick model for consumers
public sealed class PriceTick
{
    public string Instrument { get; set; } = "";
    public DateTime Time { get; set; }
    public decimal CloseoutBid { get; set; }
    public decimal CloseoutAsk { get; set; }
    public decimal Mid { get; set; }
}