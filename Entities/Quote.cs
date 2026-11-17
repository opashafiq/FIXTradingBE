
namespace TradingBackend.Api.Entities;

public class Quote
{
    public long Id { get; set; }

    // Firm ownership
    public int FirmId { get; set; }
    public Firm Firm { get; set; } = null!;

    // CHSE trading identity
    public string TradingMnemonic { get; set; } = string.Empty;

    // Instrument identity
    public string Symbol { get; set; } = string.Empty;
    public int OrderBook { get; set; }

    // Current logical quote state
    public string Status { get; set; } = "PendingSubmission";
    public bool IsActive { get; set; }

    // Latest submission reference
    public long? LatestSubmissionId { get; set; }

    // Audit
    public DateTimeOffset CreatedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAtUtc { get; set; }

 
}
