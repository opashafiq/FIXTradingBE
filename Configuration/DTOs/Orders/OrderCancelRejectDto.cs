namespace TradingBackend.Api.DTOs.Orders;

public class OrderCancelRejectDto
{
    public string SessionId { get; set; } = string.Empty;

    public string? OrderId { get; set; }
    public string? ClOrdId { get; set; }
    public string? OrigClOrdId { get; set; }
    public string? OrdStatus { get; set; }

    public int? CxlRejResponseTo { get; set; }
    public int? CxlRejReason { get; set; }

    public string? Text { get; set; }
}