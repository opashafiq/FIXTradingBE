namespace TradingBackend.Api.DTOs.Orders;

public class OrderResponseDto
{
    public long Id { get; set; }

    public string ClOrdId { get; set; } = string.Empty;

    public string? OrigClOrdId { get; set; }

    public string? ExchangeOrderId { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public int? OrderBook { get; set; }

    public string Side { get; set; } = string.Empty;

    public string? OrdType { get; set; }

    public decimal OrderQty { get; set; }

    public decimal? Price { get; set; }

    public string? TimeInForce { get; set; }

    public string? OrdStatus { get; set; }

    public decimal? LeavesQty { get; set; }

    public decimal? CumQty { get; set; }

    public decimal? AvgPx { get; set; }

    public string? Account { get; set; }

    public int? AccountType { get; set; }

    public string? OrderSource { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}