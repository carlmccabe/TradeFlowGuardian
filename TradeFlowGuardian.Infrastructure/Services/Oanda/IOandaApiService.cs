using TradeFlowGuardian.Domain.Entities;

namespace TradeFlowGuardian.Infrastructure.Services;

public interface IOandaApiService
{
    Task<decimal> GetCurrentPriceAsync(string instrument);
    Task<string> PlaceMarketOrderAsync(string instrument, int units, string side);
    Task<IEnumerable<Position>> GetPositionsAsync();
    Task<AccountSummary> GetAccountSummaryAsync();
    Task<bool> ValidateConnectionAsync();

    // NEW:
    Task<IEnumerable<Candle>> GetCandlesAsync(string instrument, string granularity, bool includeIncomplete, int count);
    Task<IEnumerable<Candle>> GetCandlesAsync(string instrument, string granularity, DateTime currentStart, DateTime chunkEnd, bool includeIncomplete);
    Task<(int PipLocation, decimal PipSize, decimal PipValuePerUnit)> GetInstrumentPipInfoAsync(string instrument);

    Task<bool> HasOpenPositionAsync(string instrument);
    Task<(long Units, decimal AveragePrice)> GetOpenPositionAsync(string instrument);

    Task<string> PlaceMarketOrderAsync(string instrument, long units, decimal? stopLoss, decimal? takeProfit,
        string clientTag);

    Task ClosePositionAsync(string instrument);

    Task<(string OrderId, string Status)> PlaceMarketOrderWithConfirmationAsync(
        string instrument,
        long units,
        decimal? stopLoss,
        decimal? takeProfit,
        string clientTag,
        decimal maxSpreadPips = 2m,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

}