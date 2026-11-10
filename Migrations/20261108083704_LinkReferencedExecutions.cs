using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class LinkReferencedExecutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ReferencedExecutionId",
                table: "executions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_executions_ReferencedExecutionId",
                table: "executions",
                column: "ReferencedExecutionId");

            migrationBuilder.AddForeignKey(
                name: "FK_executions_executions_ReferencedExecutionId",
                table: "executions",
                column: "ReferencedExecutionId",
                principalTable: "executions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_executions_executions_ReferencedExecutionId",
                table: "executions");

            migrationBuilder.DropIndex(
                name: "IX_executions_ReferencedExecutionId",
                table: "executions");

            migrationBuilder.DropColumn(
                name: "ReferencedExecutionId",
                table: "executions");
        }
    }
}
