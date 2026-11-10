namespace TradingBackend.Api.Entities;

public class FixMessageLog
{
    public long Id { get; set; }

    // FIX session information
    public string SessionId { get; set; } = string.Empty;
    public string BeginString { get; set; } = string.Empty;

    public string SenderCompID { get; set; } = string.Empty;

    public string TargetCompID { get; set; } = string.Empty;

    public string SessionQualifier { get; set; } = string.Empty;

    // FIX message information
    public string MsgType { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;
    // IN or OUT

    public string? ClOrdId { get; set; }
    public string? ExecId { get; set; }

    // Complete FIX message
    public string RawMessage { get; set; } = string.Empty;

    // Processing information
    public bool IsProcessed { get; set; }

    public string? ProcessingError { get; set; }
    public string ProcessingStatus { get; set; } = "PENDING";
    public int RetryCount { get; set; } = 0;

    public DateTimeOffset? NextRetryAtUtc { get; set; }

    public int MsgSeqNum { get; set; }

    public bool PossDupFlag { get; set; }

    public bool PossResend { get; set; }

    public string? SendingTime { get; set; }

    public string? OrigSendingTime { get; set; }
    public DateTimeOffset? ProcessingStartedAtUtc { get; set; }

    // Timestamp
    public DateTimeOffset ReceivedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;
}