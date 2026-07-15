using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using TradeFlowGuardian.Infrastructure.Data;

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
    private readonly string _connectionString = PostgresConnectionHelper.Normalize(config.Value.ConnectionString);
    
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

    private const string PairedTradesSql = """
        SELECT
            e.instrument                                        AS Instrument,
            e.direction                                         AS Direction,
            e.fill_price                                        AS EntryPrice,
            c.fill_price                                        AS ExitPrice,
            e.units                                             AS Units,
            e.executed_at                                       AS OpenedAt,
            c.executed_at                                       AS ClosedAt,
            EXTRACT(EPOCH FROM (c.executed_at - e.executed_at))::int AS DurationSeconds
        FROM trade_history e
        LEFT JOIN LATERAL (
            SELECT fill_price, executed_at
            FROM trade_history c2
            WHERE c2.instrument  = e.instrument
              AND c2.direction   = 'Close'
              AND c2.executed_at > e.executed_at
              AND c2.success     = true
            ORDER BY c2.executed_at
            LIMIT 1
        ) c ON true
        WHERE e.direction IN ('Long', 'Short')
          AND e.success    = true
          AND e.executed_at >= NOW() - INTERVAL '1 day' * @Days
        ORDER BY e.executed_at DESC
        """;

    public async Task<IReadOnlyList<TradeFlowGuardian.Core.Models.PairedTradeRecord>> GetPairedTradesAsync(
        int days = 90, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return [];

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            var rows = await conn.QueryAsync<TradeFlowGuardian.Core.Models.PairedTradeRecord>(
                PairedTradesSql, new { Days = days });
            return rows.ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query paired trade records");
            return [];
        }
    }

    // Daily realized P&L: pairs each Long/Short entry with its next Close fill, then groups
    // by the UTC day the trade was CLOSED (the day the P&L was realized) so a trade entered
    // last period but closed this period lands in this period. The window is bounded by the
    // close date >= @Start, where @Start is the current week's Monday or month's 1st (UTC).
    // Units are always positive in the DB (sizer returns positive; OandaBrokerClient negates for shorts).
    private const string DailyPnlSql = """
        SELECT
            TO_CHAR(DATE_TRUNC('day', c.executed_at AT TIME ZONE 'UTC'), 'YYYY-MM-DD') AS "Date",
            SUM(
                CASE e.direction
                    WHEN 'Long'  THEN (c.fill_price - e.fill_price) * e.units
                    WHEN 'Short' THEN (e.fill_price - c.fill_price) * e.units
                    ELSE 0
                END
            )::float AS "Pnl",
            COUNT(*)::int AS "TradeCount"
        FROM trade_history e
        JOIN LATERAL (
            SELECT fill_price, executed_at
            FROM trade_history c2
            WHERE c2.instrument  = e.instrument
              AND c2.direction   = 'Close'
              AND c2.executed_at > e.executed_at
              AND c2.success     = true
              AND c2.fill_price IS NOT NULL
            ORDER BY c2.executed_at
            LIMIT 1
        ) c ON true
        WHERE e.direction IN ('Long', 'Short')
          AND e.success     = true
          AND e.fill_price IS NOT NULL
          AND c.executed_at >= @Start
        GROUP BY 1
        ORDER BY 1 ASC
        """;

    public async Task<IReadOnlyList<TradeFlowGuardian.Core.Models.DailyPnlRecord>> GetDailyPnlAsync(
        TradeFlowGuardian.Core.Models.PnlRange range, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return [];

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            var rows = await conn.QueryAsync<TradeFlowGuardian.Core.Models.DailyPnlRecord>(
                DailyPnlSql, new { Start = PeriodStartUtc(range) });
            return rows.ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query daily P&L records");
            return [];
        }
    }

    /// <summary>
    /// First instant (UTC) of the current week (Monday 00:00) or month (1st 00:00).
    /// Matches the window the dashboard renders so chart totals reconcile exactly.
    /// </summary>
    private static DateTime PeriodStartUtc(TradeFlowGuardian.Core.Models.PnlRange range)
    {
        var today = DateTime.UtcNow.Date;
        if (range == TradeFlowGuardian.Core.Models.PnlRange.Month)
            return new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Monday-based week. DayOfWeek: Sunday = 0 … Saturday = 6.
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        return DateTime.SpecifyKind(today.AddDays(-daysSinceMonday), DateTimeKind.Utc);
    }
}
