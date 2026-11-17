namespace TradingBackend.Api.Entities;

public class QuoteStatusHistory
{
    public long Id { get; set; }

    // Logical quote
    public long QuoteId { get; set; }

    // Related submission, if identifiable
    public long? QuoteSubmissionId { get; set; }

    // Related quote entry revision, if applicable
    public long? QuoteEntryRevisionId { get; set; }

    // Exchange-reported status
    public string QuoteStatus { get; set; } = string.Empty;

    // Optional exchange rejection reason
    public string? QuoteRejectReason { get; set; }

    // Exchange response message type
    public string MsgType { get; set; } = string.Empty;

    // Exchange response time
    public DateTimeOffset? TransactTime { get; set; }

    // Original FIX message log
    public long? FixMessageLogId { get; set; }

    // Audit
    public DateTimeOffset CreatedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;
}