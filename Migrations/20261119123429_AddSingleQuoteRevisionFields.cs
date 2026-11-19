using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSingleQuoteRevisionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecInst",
                table: "quote_entry_revisions",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinQty",
                table: "quote_entry_revisions",
                type: "numeric(20,6)",
                precision: 20,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreTradeAnonymity",
                table: "quote_entry_revisions",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuoteQualifier",
                table: "quote_entry_revisions",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ValidUntilTime",
                table: "quote_entry_revisions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecInst",
                table: "quote_entry_revisions");

            migrationBuilder.DropColumn(
                name: "MinQty",
                table: "quote_entry_revisions");

            migrationBuilder.DropColumn(
                name: "PreTradeAnonymity",
                table: "quote_entry_revisions");

            migrationBuilder.DropColumn(
                name: "QuoteQualifier",
                table: "quote_entry_revisions");

            migrationBuilder.DropColumn(
                name: "ValidUntilTime",
                table: "quote_entry_revisions");
        }
    }
}
