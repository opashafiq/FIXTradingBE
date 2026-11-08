namespace TradingBackend.Api.Fix;

public class IncomingFixMessage
{
    public string SessionId { get; set; } = string.Empty;

    public string MsgType { get; set; } = string.Empty;

    public string RawMessage { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;
}