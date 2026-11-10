namespace TradingBackend.Api.Fix;

public class IncomingFixMessage
{
    public string SessionId { get; set; } = string.Empty;

    public string BeginString { get; set; } = string.Empty;

    public string SenderCompID { get; set; } = string.Empty;

    public string TargetCompID { get; set; } = string.Empty;

    public string SessionQualifier { get; set; } = string.Empty;

    public string MsgType { get; set; } = string.Empty;

    public string RawMessage { get; set; } = string.Empty;

    public int MsgSeqNum { get; set; }

    public bool PossDupFlag { get; set; }

    public bool PossResend { get; set; }

    public string? SendingTime { get; set; }

    public string? OrigSendingTime { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;
}