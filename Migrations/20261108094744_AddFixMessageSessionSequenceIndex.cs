using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFixMessageSessionSequenceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_fix_message_logs_SessionId_MsgSeqNum",
                table: "fix_message_logs",
                columns: new[] { "SessionId", "MsgSeqNum" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_fix_message_logs_SessionId_MsgSeqNum",
                table: "fix_message_logs");
        }
    }
}
