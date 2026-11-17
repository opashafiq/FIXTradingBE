using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingTokenToFixMessageLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubmissionStatus",
                table: "orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessingToken",
                table: "fix_message_logs",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmissionStatus",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ProcessingToken",
                table: "fix_message_logs");
        }
    }
}
