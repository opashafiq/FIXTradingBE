using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFixSessionIdentificationToLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BeginString",
                table: "fix_message_logs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderCompID",
                table: "fix_message_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SessionQualifier",
                table: "fix_message_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TargetCompID",
                table: "fix_message_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BeginString",
                table: "fix_message_logs");

            migrationBuilder.DropColumn(
                name: "SenderCompID",
                table: "fix_message_logs");

            migrationBuilder.DropColumn(
                name: "SessionQualifier",
                table: "fix_message_logs");

            migrationBuilder.DropColumn(
                name: "TargetCompID",
                table: "fix_message_logs");
        }
    }
}
