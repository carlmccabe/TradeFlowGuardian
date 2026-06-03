using Microsoft.EntityFrameworkCore;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Data;

public class TradeFlowDbContext(DbContextOptions<TradeFlowDbContext> options) : DbContext(options)
{
    public DbSet<RiskSettings> RiskSettings => Set<RiskSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RiskSettings>(e =>
        {
            e.ToTable("risk_settings");
            e.HasKey(r => r.Instrument);
            e.Property(r => r.Instrument).HasColumnName("instrument").HasMaxLength(20);
            e.Property(r => r.RiskPercent).HasColumnName("risk_percent").HasColumnType("numeric(5,4)");
            e.Property(r => r.IsActive).HasColumnName("is_active");
            e.Property(r => r.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            e.HasData(
                new RiskSettings { Instrument = "USD_JPY", RiskPercent = 1.5m, IsActive = true, UpdatedAt = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc) },
                new RiskSettings { Instrument = "EUR_USD", RiskPercent = 1.5m, IsActive = true, UpdatedAt = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc) },
                new RiskSettings { Instrument = "GBP_USD", RiskPercent = 1.5m, IsActive = true, UpdatedAt = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc) }
            );
        });
    }
}
