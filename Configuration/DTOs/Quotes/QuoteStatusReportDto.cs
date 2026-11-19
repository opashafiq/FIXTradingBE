
namespace TradingBackend.Api.DTOs.Quotes;

public class QuoteStatusReportDto
{
    public string SessionId { get; set; } = string.Empty;

    // CHSE FIX fields
    public string? ApplID { get; set; }

    public string? QuoteMsgID { get; set; }

    public string? BidID { get; set; }

    public string? OfferID { get; set; }

    public int? QuoteCancelType { get; set; }

    // Exchange status and rejection details
    public string? QuoteStatus { get; set; }

    public string? QuoteRejectReason { get; set; }

    public string? Text { get; set; }

    // Required by CHSE
    public DateTimeOffset? TransactTime { get; set; }

    public int? OrderBook { get; set; }
}
