using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFixMessageProcessingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessingStatus",
                table: "fix_message_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_fix_message_logs_ProcessingStatus_NextRetryAtUtc",
                table: "fix_message_logs",
                columns: new[] { "ProcessingStatus", "NextRetryAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_fix_message_logs_ProcessingStatus_NextRetryAtUtc",
                table: "fix_message_logs");

            migrationBuilder.DropColumn(
                name: "ProcessingStatus",
                table: "fix_message_logs");
        }
    }
}
