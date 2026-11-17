using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessMessageRejectRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_message_rejects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FixMessageLogId = table.Column<long>(type: "bigint", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BusinessRejectRefID = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RefSeqNum = table.Column<int>(type: "integer", nullable: true),
                    RefMsgType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RefTagID = table.Column<int>(type: "integer", nullable: true),
                    BusinessRejectReason = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    TradingOrderId = table.Column<long>(type: "bigint", nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_message_rejects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_message_rejects_fix_message_logs_FixMessageLogId",
                        column: x => x.FixMessageLogId,
                        principalTable: "fix_message_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_business_message_rejects_orders_TradingOrderId",
                        column: x => x.TradingOrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_message_rejects_FixMessageLogId",
                table: "business_message_rejects",
                column: "FixMessageLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_message_rejects_SessionId_RefSeqNum_RefMsgType",
                table: "business_message_rejects",
                columns: new[] { "SessionId", "RefSeqNum", "RefMsgType" });

            migrationBuilder.CreateIndex(
                name: "IX_business_message_rejects_TradingOrderId",
                table: "business_message_rejects",
                column: "TradingOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_message_rejects");
        }
    }
}
