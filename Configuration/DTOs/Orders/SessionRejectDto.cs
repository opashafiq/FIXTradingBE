namespace TradingBackend.Api.DTOs.Orders;

public class SessionRejectDto
{
    public string SessionId { get; set; } = string.Empty;

    public int? RefSeqNum { get; set; }

    public string? RefTagID { get; set; }

    public string? RefMsgType { get; set; }

    public int? SessionRejectReason { get; set; }

    public string? Text { get; set; }
}