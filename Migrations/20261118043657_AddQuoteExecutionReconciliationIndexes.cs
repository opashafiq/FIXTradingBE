using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteExecutionReconciliationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplId",
                table: "executions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MultiLegReportingType",
                table: "executions",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuoteCorrelationStatus",
                table: "executions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "QuoteEntryRevisionId",
                table: "executions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryOrderId",
                table: "executions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "quote_submissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirmId = table.Column<int>(type: "integer", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MsgType = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    QuoteID = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    QuoteMsgID = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    QuoteReqID = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ApplID = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Account = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SubmissionStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_submissions_Firms_FirmId",
                        column: x => x.FirmId,
                        principalTable: "Firms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quote_sets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteSubmissionId = table.Column<long>(type: "bigint", nullable: false),
                    QuoteSetID = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UnderlyingSymbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    QuoteSetValidUntilTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotNoQuoteEntries = table.Column<int>(type: "integer", nullable: false),
                    LastFragment = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_sets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_sets_quote_submissions_QuoteSubmissionId",
                        column: x => x.QuoteSubmissionId,
                        principalTable: "quote_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirmId = table.Column<int>(type: "integer", nullable: false),
                    TradingMnemonic = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OrderBook = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LatestSubmissionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quotes_Firms_FirmId",
                        column: x => x.FirmId,
                        principalTable: "Firms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quotes_quote_submissions_LatestSubmissionId",
                        column: x => x.LatestSubmissionId,
                        principalTable: "quote_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quote_entry_revisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteId = table.Column<long>(type: "bigint", nullable: false),
                    QuoteSubmissionId = table.Column<long>(type: "bigint", nullable: false),
                    QuoteSetId = table.Column<long>(type: "bigint", nullable: true),
                    QuoteEntryID = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BidPx = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: true),
                    BidSize = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    OfferPx = table.Column<decimal>(type: "numeric(20,8)", precision: 20, scale: 8, nullable: true),
                    OfferSize = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_entry_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_entry_revisions_quote_sets_QuoteSetId",
                        column: x => x.QuoteSetId,
                        principalTable: "quote_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_entry_revisions_quote_submissions_QuoteSubmissionId",
                        column: x => x.QuoteSubmissionId,
                        principalTable: "quote_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_entry_revisions_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quote_status_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteId = table.Column<long>(type: "bigint", nullable: false),
                    QuoteSubmissionId = table.Column<long>(type: "bigint", nullable: true),
                    QuoteEntryRevisionId = table.Column<long>(type: "bigint", nullable: true),
                    QuoteStatus = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    QuoteRejectReason = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MsgType = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    TransactTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FixMessageLogId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_status_history_fix_message_logs_FixMessageLogId",
                        column: x => x.FixMessageLogId,
                        principalTable: "fix_message_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_status_history_quote_entry_revisions_QuoteEntryRevisi~",
                        column: x => x.QuoteEntryRevisionId,
                        principalTable: "quote_entry_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_status_history_quote_submissions_QuoteSubmissionId",
                        column: x => x.QuoteSubmissionId,
                        principalTable: "quote_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_status_history_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_executions_PendingQuoteLegs",
                table: "executions",
                columns: new[] { "SessionId", "ClOrdId", "SecondaryOrderId" },
                filter: "\"MultiLegReportingType\" = '2' AND \"QuoteCorrelationStatus\" = 'UNRESOLVED' AND \"QuoteEntryRevisionId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_executions_QuoteCorrelationStatus",
                table: "executions",
                column: "QuoteCorrelationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_executions_QuoteEntryRevisionId",
                table: "executions",
                column: "QuoteEntryRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_executions_QuoteParentCorrelation",
                table: "executions",
                columns: new[] { "SessionId", "ClOrdId", "ExchangeOrderId" },
                filter: "\"MultiLegReportingType\" = '3' AND \"QuoteEntryRevisionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_quote_entry_revisions_QuoteId",
                table: "quote_entry_revisions",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_entry_revisions_QuoteId_CreatedAtUtc",
                table: "quote_entry_revisions",
                columns: new[] { "QuoteId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_entry_revisions_QuoteSetId",
                table: "quote_entry_revisions",
                column: "QuoteSetId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_entry_revisions_QuoteSubmissionId",
                table: "quote_entry_revisions",
                column: "QuoteSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_entry_revisions_QuoteSubmissionId_QuoteSetId_QuoteEnt~",
                table: "quote_entry_revisions",
                columns: new[] { "QuoteSubmissionId", "QuoteSetId", "QuoteEntryID" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_sets_QuoteSubmissionId",
                table: "quote_sets",
                column: "QuoteSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_sets_QuoteSubmissionId_QuoteSetID",
                table: "quote_sets",
                columns: new[] { "QuoteSubmissionId", "QuoteSetID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quote_status_history_FixMessageLogId",
                table: "quote_status_history",
                column: "FixMessageLogId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_status_history_QuoteEntryRevisionId",
                table: "quote_status_history",
                column: "QuoteEntryRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_status_history_QuoteId",
                table: "quote_status_history",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_status_history_QuoteId_CreatedAtUtc",
                table: "quote_status_history",
                columns: new[] { "QuoteId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_status_history_QuoteSubmissionId",
                table: "quote_status_history",
                column: "QuoteSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_submissions_CreatedAtUtc",
                table: "quote_submissions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_quote_submissions_FirmId",
                table: "quote_submissions",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_submissions_QuoteID",
                table: "quote_submissions",
                column: "QuoteID");

            migrationBuilder.CreateIndex(
                name: "IX_quote_submissions_QuoteMsgID",
                table: "quote_submissions",
                column: "QuoteMsgID");

            migrationBuilder.CreateIndex(
                name: "IX_quote_submissions_SessionId_MsgType",
                table: "quote_submissions",
                columns: new[] { "SessionId", "MsgType" });

            migrationBuilder.CreateIndex(
                name: "IX_quotes_FirmId",
                table: "quotes",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_FirmId_TradingMnemonic_Symbol_OrderBook",
                table: "quotes",
                columns: new[] { "FirmId", "TradingMnemonic", "Symbol", "OrderBook" });

            migrationBuilder.CreateIndex(
                name: "IX_quotes_LatestSubmissionId",
                table: "quotes",
                column: "LatestSubmissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_executions_quote_entry_revisions_QuoteEntryRevisionId",
                table: "executions",
                column: "QuoteEntryRevisionId",
                principalTable: "quote_entry_revisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_executions_quote_entry_revisions_QuoteEntryRevisionId",
                table: "executions");

            migrationBuilder.DropTable(
                name: "quote_status_history");

            migrationBuilder.DropTable(
                name: "quote_entry_revisions");

            migrationBuilder.DropTable(
                name: "quote_sets");

            migrationBuilder.DropTable(
                name: "quotes");

            migrationBuilder.DropTable(
                name: "quote_submissions");

            migrationBuilder.DropIndex(
                name: "IX_executions_PendingQuoteLegs",
                table: "executions");

            migrationBuilder.DropIndex(
                name: "IX_executions_QuoteCorrelationStatus",
                table: "executions");

            migrationBuilder.DropIndex(
                name: "IX_executions_QuoteEntryRevisionId",
                table: "executions");

            migrationBuilder.DropIndex(
                name: "IX_executions_QuoteParentCorrelation",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "ApplId",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "MultiLegReportingType",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "QuoteCorrelationStatus",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "QuoteEntryRevisionId",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "SecondaryOrderId",
                table: "executions");
        }
    }
}
