
namespace TradingBackend.Api.DTOs.Orders;

public class BusinessMessageRejectDto
{
    public string SessionId { get; set; } = string.Empty;

    public string? BusinessRejectRefID { get; set; }

    public int? RefSeqNum { get; set; }

    public string? RefMsgType { get; set; }

    public int? RefTagID { get; set; }

    public int? BusinessRejectReason { get; set; }

    public string? Text { get; set; }
}
