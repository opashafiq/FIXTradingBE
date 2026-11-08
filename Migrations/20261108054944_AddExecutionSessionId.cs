using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_executions_ExecId",
                table: "executions");

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "executions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_executions_SessionId_ExecId",
                table: "executions",
                columns: new[] { "SessionId", "ExecId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_executions_SessionId_ExecId",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "executions");

            migrationBuilder.CreateIndex(
                name: "IX_executions_ExecId",
                table: "executions",
                column: "ExecId",
                unique: true);
        }
    }
}
