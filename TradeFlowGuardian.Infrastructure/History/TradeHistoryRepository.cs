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
    private readonly string _connectionString = NormalizeConnectionString(config.Value.ConnectionString);
    
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

    public async Task<(bool Reachable, long RowCount, string? Error)> GetStatusAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return (false, 0, "Postgres:ConnectionString is not configured");

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            var count = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM trade_history");
            return (true, count, null);
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }

    private static string NormalizeConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return string.Empty;

        if (!connectionString.Contains("://", StringComparison.Ordinal))
            return connectionString;

        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
            return connectionString;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]),
            Password = uri.UserInfo.Contains(':')
                ? Uri.UnescapeDataString(uri.UserInfo.Split(':', 2)[1])
                : string.Empty,
            SslMode = SslMode.Require,
        };

        return builder.ConnectionString;
    }
}
