namespace TradingBackend.Api.Entities;

public class QuoteEntryRevision
{
    public long Id { get; set; }

    // Logical quote identity
    public long QuoteId { get; set; }

    // Originating FIX submission
    public long QuoteSubmissionId { get; set; }

    // Mass Quote set; null for Single Quote
    public long? QuoteSetId { get; set; }

    // CHSE Mass Quote entry identifier
    public string? QuoteEntryID { get; set; }

    // Instrument identity
    public string Symbol { get; set; } = string.Empty;

    // Bid side
    public decimal? BidPx { get; set; }
    public decimal? BidSize { get; set; }

    // Offer side
    public decimal? OfferPx { get; set; }
    public decimal? OfferSize { get; set; }

    // Revision lifecycle
    public string Status { get; set; } = "PendingSubmission";

    // Audit
    public DateTimeOffset CreatedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;
}