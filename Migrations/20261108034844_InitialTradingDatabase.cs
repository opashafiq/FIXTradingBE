using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialTradingDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fix_message_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MsgType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Direction = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ClOrdId = table.Column<string>(type: "text", nullable: true),
                    ExecId = table.Column<string>(type: "text", nullable: true),
                    RawMessage = table.Column<string>(type: "text", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessingError = table.Column<string>(type: "text", nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fix_message_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<string>(type: "text", nullable: true),
                    ClOrdId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrigClOrdId = table.Column<string>(type: "text", nullable: true),
                    ExchangeOrderId = table.Column<string>(type: "text", nullable: true),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OrderBook = table.Column<int>(type: "integer", nullable: true),
                    Side = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    OrdType = table.Column<string>(type: "text", nullable: true),
                    OrderQty = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: true),
                    TimeInForce = table.Column<string>(type: "text", nullable: true),
                    OrdStatus = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    LeavesQty = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    CumQty = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    AvgPx = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: true),
                    Account = table.Column<string>(type: "text", nullable: true),
                    AccountType = table.Column<int>(type: "integer", nullable: true),
                    OrderSource = table.Column<string>(type: "text", nullable: true),
                    TransactTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "executions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExecId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExecRefId = table.Column<string>(type: "text", nullable: true),
                    ClOrdId = table.Column<string>(type: "text", nullable: true),
                    ExchangeOrderId = table.Column<string>(type: "text", nullable: true),
                    ExecType = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    OrdStatus = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    LastQty = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    LastPx = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: true),
                    CumQty = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    LeavesQty = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    AvgPx = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: true),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Side = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    OrderBook = table.Column<int>(type: "integer", nullable: true),
                    TransactTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TradingOrderId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_executions_orders_TradingOrderId",
                        column: x => x.TradingOrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_executions_ExecId",
                table: "executions",
                column: "ExecId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_executions_TradingOrderId",
                table: "executions",
                column: "TradingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_fix_message_logs_ClOrdId",
                table: "fix_message_logs",
                column: "ClOrdId");

            migrationBuilder.CreateIndex(
                name: "IX_fix_message_logs_ExecId",
                table: "fix_message_logs",
                column: "ExecId");

            migrationBuilder.CreateIndex(
                name: "IX_fix_message_logs_ReceivedAtUtc",
                table: "fix_message_logs",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_orders_ExchangeOrderId",
                table: "orders",
                column: "ExchangeOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_Symbol",
                table: "orders",
                column: "Symbol");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "executions");

            migrationBuilder.DropTable(
                name: "fix_message_logs");

            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
