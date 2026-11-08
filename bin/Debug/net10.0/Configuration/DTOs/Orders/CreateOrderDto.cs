namespace TradingBackend.Api.DTOs.Orders;

public class CreateOrderDto
{
    public string ClOrdId { get; set; } = string.Empty;

    public string? OrigClOrdId { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public int? OrderBook { get; set; }

    public string Side { get; set; } = string.Empty;

    public string? OrdType { get; set; }

    public decimal OrderQty { get; set; }

    public decimal? Price { get; set; }

    public string? TimeInForce { get; set; }

    public string? Account { get; set; }

    public int? AccountType { get; set; }

    public string? OrderSource { get; set; }
}