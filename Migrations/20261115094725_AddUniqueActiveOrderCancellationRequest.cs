using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBackend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueActiveOrderCancellationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_cancellation_requests_TradingOrderId",
                table: "order_cancellation_requests");

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellation_requests_TradingOrderId_Active",
                table: "order_cancellation_requests",
                column: "TradingOrderId",
                unique: true,
                filter: "\"Status\" IN ('PendingSubmission', 'SubmittedToFix', 'SubmissionUnknown')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_cancellation_requests_TradingOrderId_Active",
                table: "order_cancellation_requests");

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellation_requests_TradingOrderId",
                table: "order_cancellation_requests",
                column: "TradingOrderId");
        }
    }
}
