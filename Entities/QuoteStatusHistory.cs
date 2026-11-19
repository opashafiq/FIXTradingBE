
namespace TradingBackend.Api.Entities;

public class QuoteStatusHistory
{
    public long Id { get; set; }

    public long QuoteId { get; set; }

    public long? QuoteSubmissionId { get; set; }

    public long? QuoteEntryRevisionId { get; set; }

    public string QuoteStatus { get; set; } = string.Empty;

    public string? QuoteRejectReason { get; set; }

    public string MsgType { get; set; } = string.Empty;

    public DateTimeOffset? TransactTime { get; set; }

    public long? FixMessageLogId { get; set; }

    // Exchange response details
    public string? ApplID { get; set; }

    public string? BidID { get; set; }

    public string? OfferID { get; set; }

    public string? Text { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;
}
