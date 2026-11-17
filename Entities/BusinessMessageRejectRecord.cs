
namespace TradingBackend.Api.Entities;

public class BusinessMessageRejectRecord
{
    public long Id { get; set; }

    // The incoming FIX message log that contains this rejection.
    public long FixMessageLogId { get; set; }

    public string SessionId { get; set; } = string.Empty;

    // FIX tag 379
    public string? BusinessRejectRefID { get; set; }

    // FIX tag 45
    public int? RefSeqNum { get; set; }

    // FIX tag 372
    public string? RefMsgType { get; set; }

    // FIX tag 371
    public int? RefTagID { get; set; }

    // FIX tag 380
    public int BusinessRejectReason { get; set; }

    // FIX tag 58
    public string? Text { get; set; }

    // Matched order, if correlation succeeds.
    public long? TradingOrderId { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
