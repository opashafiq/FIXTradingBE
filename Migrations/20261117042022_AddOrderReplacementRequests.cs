using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReplacementRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_replacement_requests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradingOrderId = table.Column<long>(type: "bigint", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ClOrdId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OrigClOrdId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OrderQty = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: true),
                    StopPx = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: true),
                    TimeInForce = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    ExpireDate = table.Column<DateTime>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubmissionError = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_replacement_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_replacement_requests_orders_TradingOrderId",
                        column: x => x.TradingOrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_replacement_requests_SessionId_ClOrdId",
                table: "order_replacement_requests",
                columns: new[] { "SessionId", "ClOrdId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_replacement_requests_TradingOrderId_Active",
                table: "order_replacement_requests",
                column: "TradingOrderId",
                unique: true,
                filter: "\"Status\" IN ('PendingSubmission', 'SubmittedToFix', 'SubmissionUnknown', 'PendingReplace')");

            migrationBuilder.CreateIndex(
                name: "IX_order_replacement_requests_TradingOrderId_Status",
                table: "order_replacement_requests",
                columns: new[] { "TradingOrderId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_replacement_requests");
        }
    }
}
