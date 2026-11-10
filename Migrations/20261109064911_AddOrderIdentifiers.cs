using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_identifiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradingOrderId = table.Column<long>(type: "bigint", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClOrdId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OrigClOrdId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_identifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_identifiers_orders_TradingOrderId",
                        column: x => x.TradingOrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_identifiers_SessionId_ClOrdId",
                table: "order_identifiers",
                columns: new[] { "SessionId", "ClOrdId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_identifiers_TradingOrderId",
                table: "order_identifiers",
                column: "TradingOrderId");

            migrationBuilder.Sql("""
                INSERT INTO order_identifiers
                (
                    "TradingOrderId",
                    "SessionId",
                    "ClOrdId",
                    "OrigClOrdId",
                    "CreatedAtUtc"
                )
                SELECT
                    "Id",
                    "SessionId",
                    "ClOrdId",
                    "OrigClOrdId",
                    "CreatedAtUtc"
                FROM orders
                WHERE "ClOrdId" IS NOT NULL
                AND "ClOrdId" <> '';
                """);                
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_identifiers");
        }
    }
}
