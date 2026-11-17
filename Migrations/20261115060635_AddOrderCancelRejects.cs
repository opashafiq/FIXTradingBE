using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCancelRejects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            

            migrationBuilder.CreateTable(
                name: "order_cancel_rejects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClOrdId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OrigClOrdId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OrdStatus = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CxlRejResponseTo = table.Column<int>(type: "integer", nullable: true),
                    CxlRejReason = table.Column<int>(type: "integer", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: true),
                    TradingOrderId = table.Column<long>(type: "bigint", nullable: true),
                    FixMessageLogId = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_cancel_rejects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_cancel_rejects_fix_message_logs_FixMessageLogId",
                        column: x => x.FixMessageLogId,
                        principalTable: "fix_message_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_cancel_rejects_orders_TradingOrderId",
                        column: x => x.TradingOrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_cancel_rejects_FixMessageLogId",
                table: "order_cancel_rejects",
                column: "FixMessageLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_cancel_rejects_SessionId_ClOrdId",
                table: "order_cancel_rejects",
                columns: new[] { "SessionId", "ClOrdId" });

            migrationBuilder.CreateIndex(
                name: "IX_order_cancel_rejects_TradingOrderId",
                table: "order_cancel_rejects",
                column: "TradingOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_cancel_rejects");

            
        }
    }
}
