using Microsoft.EntityFrameworkCore;
using TradeFlowGuardian.Core.Models;

namespace TradeFlowGuardian.Infrastructure.Data;

public class TradeFlowDbContext(DbContextOptions<TradeFlowDbContext> options) : DbContext(options)
{
    public DbSet<RiskSettings> RiskSettings => Set<RiskSettings>();
    public DbSet<OandaAccount> OandaAccounts => Set<OandaAccount>();

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

        modelBuilder.Entity<OandaAccount>(e =>
        {
            e.ToTable("oanda_accounts");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.Label).HasColumnName("label").HasMaxLength(100);
            e.Property(a => a.AccountId).HasColumnName("account_id").HasMaxLength(50);
            e.Property(a => a.Environment).HasColumnName("environment").HasMaxLength(20);
            e.Property(a => a.ApiKeyEncrypted).HasColumnName("api_key_encrypted");
            e.Property(a => a.IsActive).HasColumnName("is_active");
            e.Property(a => a.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            e.Property(a => a.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            // Mirrors ux_oanda_accounts_one_active in 004_oanda_accounts.sql
            e.HasIndex(a => a.IsActive)
                .IsUnique()
                .HasFilter("is_active")
                .HasDatabaseName("ux_oanda_accounts_one_active");
            e.HasIndex(a => new { a.AccountId, a.Environment })
                .IsUnique()
                .HasDatabaseName("ux_oanda_accounts_account_env");
        });
    }
}
