
namespace TradingBackend.Api.DTOs.Orders;

public class CreateOrderDto
{
    // Order identification
    public string ClOrdId { get; set; } = string.Empty;
    public string? OrigClOrdId { get; set; }

    // Instrument
    public string Symbol { get; set; } = string.Empty;
    public int? OrderBook { get; set; }

    // Order details
    public string Side { get; set; } = string.Empty;
    public string? OrdType { get; set; }
    public decimal OrderQty { get; set; }
    public decimal? Price { get; set; }
    public decimal? StopPx { get; set; }

    // Time in Force
    public string? TimeInForce { get; set; }
    public DateTime? ExpireDate { get; set; }

    // Client account information
    public string? ClientCode { get; set; }
    public string? BoId { get; set; }
    public int? AccountType { get; set; }

    // Trading information
    public string? OrderSource { get; set; }
    public string? NoTradeKey { get; set; }
}
