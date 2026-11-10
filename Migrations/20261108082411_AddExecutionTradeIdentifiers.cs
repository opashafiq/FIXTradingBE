using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionTradeIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecRestatementReason",
                table: "executions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MdEntryId",
                table: "executions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrdRejReason",
                table: "executions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderCapacity",
                table: "executions",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryTradeId",
                table: "executions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrdMatchId",
                table: "executions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecRestatementReason",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "MdEntryId",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "OrdRejReason",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "OrderCapacity",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "SecondaryTradeId",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "TrdMatchId",
                table: "executions");
        }
    }
}
