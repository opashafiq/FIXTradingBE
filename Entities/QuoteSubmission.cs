
namespace TradingBackend.Api.Entities;

public class QuoteSubmission
{
    public long Id { get; set; }

    // Firm ownership
    public int FirmId { get; set; }
    public Firm Firm { get; set; } = null!;

    // FIX session and message identity
    public string SessionId { get; set; } = string.Empty;
    public string MsgType { get; set; } = string.Empty;

    // CHSE quote identifiers
    public string? QuoteID { get; set; }
    public string? QuoteMsgID { get; set; }
    public string? QuoteReqID { get; set; }
    public string? ApplID { get; set; }

    // Trading identity
    public string? Account { get; set; }

    // Transport submission status
    public string SubmissionStatus { get; set; }
        = "PendingSubmission";

    // Audit
    public DateTimeOffset CreatedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset? SubmittedAtUtc { get; set; }
}
