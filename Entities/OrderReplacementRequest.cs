
namespace TradingBackend.Api.Entities;

public class OrderReplacementRequest
{
    public long Id { get; set; }

    public long TradingOrderId { get; set; }
    public TradingOrder TradingOrder { get; set; } = null!;

    public string SessionId { get; set; } = string.Empty;

    // New identifier generated for the replacement request.
    public string ClOrdId { get; set; } = string.Empty;

    // Previous order identifier being replaced.
    public string OrigClOrdId { get; set; } = string.Empty;

    // Effective requested values, including unchanged values.
    public decimal OrderQty { get; set; }
    public decimal? Price { get; set; }
    public decimal? StopPx { get; set; }
    public string? TimeInForce { get; set; }
    public DateTime? ExpireDate { get; set; }

    public string Status { get; set; } = "PendingSubmission";

    public string? SubmissionError { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
