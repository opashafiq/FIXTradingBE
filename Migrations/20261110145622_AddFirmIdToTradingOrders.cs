using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFirmIdToTradingOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FirmId",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_FirmId",
                table: "orders",
                column: "FirmId");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_Firms_FirmId",
                table: "orders",
                column: "FirmId",
                principalTable: "Firms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_Firms_FirmId",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_FirmId",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FirmId",
                table: "orders");
        }
    }
}
