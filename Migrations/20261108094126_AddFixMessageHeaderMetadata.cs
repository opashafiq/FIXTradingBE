using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFixMessageHeaderMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MsgSeqNum",
                table: "fix_message_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OrigSendingTime",
                table: "fix_message_logs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PossDupFlag",
                table: "fix_message_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PossResend",
                table: "fix_message_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SendingTime",
                table: "fix_message_logs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MsgSeqNum",
                table: "fix_message_logs");

            migrationBuilder.DropColumn(
                name: "OrigSendingTime",
                table: "fix_message_logs");

            migrationBuilder.DropColumn(
                name: "PossDupFlag",
                table: "fix_message_logs");

            migrationBuilder.DropColumn(
                name: "PossResend",
                table: "fix_message_logs");

            migrationBuilder.DropColumn(
                name: "SendingTime",
                table: "fix_message_logs");
        }
    }
}
