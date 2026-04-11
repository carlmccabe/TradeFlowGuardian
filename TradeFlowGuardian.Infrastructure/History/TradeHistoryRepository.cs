using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.History;

/// <summary>
/// Npgsql + Dapper implementation of <see cref="ITradeHistoryRepository"/>.
/// Creates a new connection per call — suitable for low-frequency trade writes.
/// Never throws; logs errors and returns so callers are unaffected by DB outages.
/// </summary>
public class TradeHistoryRepository(
    IOptions<PostgresConfig> config,
    ILogger<TradeHistoryRepository> logger) : ITradeHistoryRepository
{
    private readonly string _connectionString = config.Value.ConnectionString;

    private const string InsertSql = """
        INSERT INTO trade_history
            (instrument, direction, entry_price, sl, tp, units,
             fill_price, order_id, success, error_message, executed_at)
        VALUES
            (@Instrument, @Direction, @EntryPrice, @StopLoss, @TakeProfit, @Units,
             @FillPrice, @OrderId, @Success, @ErrorMessage, @ExecutedAt)
        """;

    public async Task InsertAsync(TradeHistoryRecord record, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            logger.LogWarning("Trade history not persisted for {Instrument}: Postgres:ConnectionString is not configured", record.Instrument);
            return;
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await conn.ExecuteAsync(InsertSql, record);
        }
        catch (Exception ex)
        {
            // Log and swallow — a history write failure must never abort the trade workflow.
            // The order has already been placed; losing a history row is preferable to
            // masking the fill from the caller.
            logger.LogError(ex,
                "Failed to write trade history for {Instrument} {Direction}: {Message}",
                record.Instrument, record.Direction, ex.Message);
        }
    }
}
