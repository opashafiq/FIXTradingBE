
namespace TradingBackend.Api.Entities;

public class OrderCancelReject
{
    public long Id { get; set; }

    // FIX session
    public string SessionId { get; set; } = string.Empty;

    // Exchange identifiers
    public string? OrderId { get; set; }
    public string? ClOrdId { get; set; }
    public string? OrigClOrdId { get; set; }

    // Order state at the time of rejection
    public string? OrdStatus { get; set; }

    // 434: 1 = Order Cancel Request, 2 = Order Cancel/Replace Request
    public int? CxlRejResponseTo { get; set; }

    // 102: CHSE cancellation rejection reason
    public int? CxlRejReason { get; set; }

    // 58: Exchange explanation
    public string? Text { get; set; }

    // Associated trading order
    public long? TradingOrderId { get; set; }
    public TradingOrder? TradingOrder { get; set; }

    // Original incoming FIX message
    public long FixMessageLogId { get; set; }
    public FixMessageLog FixMessageLog { get; set; } = null!;

    public DateTimeOffset ReceivedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;
}
