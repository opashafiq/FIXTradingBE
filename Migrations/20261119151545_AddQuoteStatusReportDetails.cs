using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteStatusReportDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplID",
                table: "quote_status_history",
                type: "character varying(128)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BidID",
                table: "quote_status_history",
                type: "character varying(128)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfferID",
                table: "quote_status_history",
                type: "character varying(128)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "quote_status_history",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplID",
                table: "quote_status_history");

            migrationBuilder.DropColumn(
                name: "BidID",
                table: "quote_status_history");

            migrationBuilder.DropColumn(
                name: "OfferID",
                table: "quote_status_history");

            migrationBuilder.DropColumn(
                name: "Text",
                table: "quote_status_history");
        }
    }
}
