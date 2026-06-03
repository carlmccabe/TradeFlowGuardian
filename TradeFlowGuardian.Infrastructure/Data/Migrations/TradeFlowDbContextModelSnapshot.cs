using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TradeFlowGuardian.Infrastructure.Data;

#nullable disable

namespace TradeFlowGuardian.Infrastructure.Data.Migrations
{
    [DbContext(typeof(TradeFlowDbContext))]
    partial class TradeFlowDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("TradeFlowGuardian.Core.Models.RiskSettings", b =>
            {
                b.Property<string>("Instrument")
                    .HasMaxLength(20)
                    .HasColumnType("character varying(20)")
                    .HasColumnName("instrument");

                b.Property<bool>("IsActive")
                    .HasColumnType("boolean")
                    .HasColumnName("is_active");

                b.Property<decimal>("RiskPercent")
                    .HasColumnType("numeric(5,4)")
                    .HasColumnName("risk_percent");

                b.Property<DateTime>("UpdatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("updated_at");

                b.HasKey("Instrument");

                b.ToTable("risk_settings");

                b.HasData(
                    new
                    {
                        Instrument = "EUR_USD",
                        IsActive = true,
                        RiskPercent = 1.5m,
                        UpdatedAt = new DateTime(2026, 6, 3, 0, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new
                    {
                        Instrument = "GBP_USD",
                        IsActive = true,
                        RiskPercent = 1.5m,
                        UpdatedAt = new DateTime(2026, 6, 3, 0, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new
                    {
                        Instrument = "USD_JPY",
                        IsActive = true,
                        RiskPercent = 1.5m,
                        UpdatedAt = new DateTime(2026, 6, 3, 0, 0, 0, 0, DateTimeKind.Utc)
                    });
            });
#pragma warning restore 612, 618
        }
    }
}
