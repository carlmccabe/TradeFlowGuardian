using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace TradeFlowGuardian.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitRiskSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "risk_settings",
                columns: table => new
                {
                    instrument = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    risk_percent = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    is_active = table.Column<bool>(nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_settings", x => x.instrument);
                });

            migrationBuilder.InsertData(
                table: "risk_settings",
                columns: new[] { "instrument", "is_active", "risk_percent", "updated_at" },
                values: new object[,]
                {
                    { "EUR_USD", true, 1.5m, new DateTime(2026, 6, 3, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "GBP_USD", true, 1.5m, new DateTime(2026, 6, 3, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "USD_JPY", true, 1.5m, new DateTime(2026, 6, 3, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "risk_settings");
        }
    }
}
