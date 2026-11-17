
namespace TradingBackend.Api.Entities;

public class OrderCancellationRequest
{
    public long Id { get; set; }

    // Original trading order.
    public long TradingOrderId { get; set; }
    public TradingOrder TradingOrder { get; set; } = null!;

    // FIX session and cancellation identifiers.
    public string SessionId { get; set; } = string.Empty;

    // New ClOrdID generated for this cancellation request.
    public string ClOrdId { get; set; } = string.Empty;

    // ClOrdID of the order version being cancelled.
    public string? OrigClOrdId { get; set; }

    // PendingSubmission, SubmittedToFix, SubmissionUnknown,
    // Confirmed, Rejected.
    public string Status { get; set; } = "PendingSubmission";

    // Diagnostic information when submission is uncertain or fails.
    public string? SubmissionError { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
