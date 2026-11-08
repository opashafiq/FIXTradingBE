namespace TradingBackend.Api.Entities;

public class FixMessageLog
{
    public long Id { get; set; }

    // FIX session information
    public string SessionId { get; set; } = string.Empty;

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

    // Timestamp
    public DateTimeOffset ReceivedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;
}