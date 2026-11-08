namespace TradingBackend.Api.DTOs.Orders;

public class UpdateOrderDto
{
    public string? OrdStatus { get; set; }

    public string? ExchangeOrderId { get; set; }

    public decimal? LeavesQty { get; set; }

    public decimal? CumQty { get; set; }

    public decimal? AvgPx { get; set; }
}