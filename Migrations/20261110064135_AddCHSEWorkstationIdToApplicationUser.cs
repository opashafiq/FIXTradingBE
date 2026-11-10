using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCHSEWorkstationIdToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CHSEWorkstationId",
                table: "AspNetUsers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CHSEWorkstationId",
                table: "AspNetUsers",
                column: "CHSEWorkstationId",
                unique: true,
                filter: "\"CHSEWorkstationId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CHSEWorkstationId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CHSEWorkstationId",
                table: "AspNetUsers");
        }
    }
}
