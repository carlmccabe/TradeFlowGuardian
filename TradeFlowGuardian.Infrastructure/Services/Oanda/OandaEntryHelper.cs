using TradeFlowGuardian.Infrastructure.Services.Oanda.StreamingModels;

namespace TradeFlowGuardian.Infrastructure.Services.Oanda
{
    // Helper demonstrating the recommended pattern:
    // 1) Ensure stream is started
    // 2) Place order via IOandaApiService and get OrderId
    // 3) Await ORDER_FILL on the transactions stream using TaskCompletionSource
    // 4) On timeout optionally cancel via REST
    public class OandaEntryHelper : IDisposable
    {
        private readonly IOandaApiService _oanda;
        private readonly OandaStreamingService _stream;

        public OandaEntryHelper(IOandaApiService oandaApiService, OandaStreamingService streamingService)
        {
            _oanda = oandaApiService;
            _stream = streamingService;
        }

        public void StartStreamIfNeeded(string accessToken, string accountId, CancellationToken ct = default)
        {
            _stream.StartTransactionsStream(accessToken, accountId, ct);
        }

        public async Task<bool> PlaceMarketOrderAndWaitForFillAsync(
            string accountId,
            string instrument,
            long units,
            decimal? stopLoss,
            decimal? takeProfit,
            string clientTag,
            string accessToken,
            TimeSpan timeout)
        {
            StartStreamIfNeeded(accessToken, accountId);

            // Use the PlaceMarketOrderWithConfirmationAsync overload on IOandaApiService which returns (OrderId, Status)
            var placeResult = await _oanda.PlaceMarketOrderWithConfirmationAsync(
                instrument,
                units,
                stopLoss,
                takeProfit,
                clientTag).ConfigureAwait(false);

            var orderId = placeResult.OrderId;
            if (string.IsNullOrEmpty(orderId))
                return false;

            var tcs = new TaskCompletionSource<TransactionEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(TransactionEvent evt)
            {
                if (evt.OrderID == orderId)
                {
                    if (string.Equals(evt.Type, "ORDER_FILL", StringComparison.OrdinalIgnoreCase))
                        tcs.TrySetResult(evt);
                    else if (string.Equals(evt.Type, "ORDER_CANCEL", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(evt.Type, "ORDER_REJECT", StringComparison.OrdinalIgnoreCase))
                        tcs.TrySetException(new Exception($"Order {orderId} not filled: {evt.Type}"));
                }
            }

            _stream.TransactionReceived += Handler;

            try
            {
                var delay = Task.Delay(timeout);
                var completed = await Task.WhenAny(tcs.Task, delay).ConfigureAwait(false);

                if (completed == tcs.Task)
                {
                    var fill = await tcs.Task.ConfigureAwait(false);
                    // handle fill: persist trade state, etc.
                    return true;
                }
                else
                {
                    // timeout: best-effort cancel
                    try { await _oanda.ClosePositionAsync(instrument).ConfigureAwait(false); } catch { /* ignore */ }
                    return false;
                }
            }
            finally
            {
                _stream.TransactionReceived -= Handler;
            }
        }

        public void Dispose()
        {
            // Do not dispose _stream if it's a singleton. Manage lifecycle in composition root.
        }
    }
}