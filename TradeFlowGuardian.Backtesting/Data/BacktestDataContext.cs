using TradeFlowGuardian.Backtesting.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace TradeFlowGuardian.Backtesting.Data;

public class BacktestDataContext(DbContextOptions<BacktestDataContext> options) : DbContext(options)
{
    public DbSet<HistoricalCandle> HistoricalCandles { get; set; }
    public DbSet<BacktestRun> BacktestRuns { get; set; }
    public DbSet<BacktestTradeEntity> BacktestTrades { get; set; }
    public DbSet<BacktestEquityPoint> BacktestEquityCurve { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // HistoricalCandles configuration
        modelBuilder.Entity<HistoricalCandle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Open).HasPrecision(18, 8);
            entity.Property(e => e.High).HasPrecision(18, 8);
            entity.Property(e => e.Low).HasPrecision(18, 8);
            entity.Property(e => e.Close).HasPrecision(18, 8);
            entity.Property(e => e.Spread).HasPrecision(10, 6);
            
            // Composite index for fast backtesting queries
            entity.HasIndex(e => new { e.Instrument, e.Timeframe, e.Timestamp })
                  .HasDatabaseName("IX_Instrument_Timeframe_Timestamp");

            // Index for time-based queries
            entity.HasIndex(e => e.Timestamp)
                  .HasDatabaseName("IX_Timestamp");
        });

        // BacktestRun configuration
        modelBuilder.Entity<BacktestRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InitialBalance).HasPrecision(18, 2);
            entity.Property(e => e.FinalBalance).HasPrecision(18, 2);
            entity.Property(e => e.TotalReturn).HasPrecision(10, 6);
            entity.Property(e => e.MaxDrawdown).HasPrecision(10, 6);
            entity.Property(e => e.SharpeRatio).HasPrecision(10, 4);
            entity.Property(e => e.SortinoRatio).HasPrecision(10, 4);
            entity.Property(e => e.CalmarRatio).HasPrecision(10, 4);
            entity.Property(e => e.ProfitFactor).HasPrecision(10, 4);
            entity.Property(e => e.WinRate).HasPrecision(10, 6);
            entity.Property(e => e.AverageWin).HasPrecision(18, 6);
            entity.Property(e => e.AverageLoss).HasPrecision(18, 6);
            entity.Property(e => e.LargestWin).HasPrecision(18, 6);
            entity.Property(e => e.LargestLoss).HasPrecision(18, 6);

            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.StrategyName, e.Instrument });
        });

        // BacktestTradeEntity configuration
        modelBuilder.Entity<BacktestTradeEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntryPrice).HasPrecision(18, 8);
            entity.Property(e => e.ExitPrice).HasPrecision(18, 8);
            entity.Property(e => e.Units).HasPrecision(18, 2);
            entity.Property(e => e.StopLoss).HasPrecision(18, 8);
            entity.Property(e => e.TakeProfit).HasPrecision(18, 8);
            entity.Property(e => e.PnL).HasPrecision(18, 6);
            entity.Property(e => e.PnLPercent).HasPrecision(10, 6);
            entity.Property(e => e.Commission).HasPrecision(18, 6);
            entity.Property(e => e.Slippage).HasPrecision(18, 6);
            entity.Property(e => e.MAE).HasPrecision(18, 6);
            entity.Property(e => e.MFE).HasPrecision(18, 6);

            entity.HasOne(e => e.BacktestRun)
                  .WithMany(r => r.Trades)
                  .HasForeignKey(e => e.BacktestRunId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.BacktestRunId, e.TradeNumber });
            entity.HasIndex(e => e.EntryTime);
            entity.HasIndex(e => e.PnL);
        });

        // BacktestEquityPoint configuration  
        modelBuilder.Entity<BacktestEquityPoint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Balance).HasPrecision(18, 6);
            entity.Property(e => e.Equity).HasPrecision(18, 6);
            entity.Property(e => e.DrawdownPercent).HasPrecision(10, 6);

            entity.HasOne(e => e.BacktestRun)
                  .WithMany(r => r.EquityCurve)
                  .HasForeignKey(e => e.BacktestRunId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.BacktestRunId, e.Timestamp });
        });
    }
}