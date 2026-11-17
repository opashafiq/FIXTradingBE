
namespace TradingBackend.Api.DTOs.Orders;

public class ReplaceOrderDto
{
    // Editable order parameters
    public decimal? OrderQty { get; set; }

    public decimal? Price { get; set; }

    public decimal? StopPx { get; set; }

    // Time in Force
    public string? TimeInForce { get; set; }

    public DateTime? ExpireDate { get; set; }
}
