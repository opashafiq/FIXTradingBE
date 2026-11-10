namespace TradingBackend.Api.Entities;

public class TradingOrder
{
    public long Id { get; set; }

    // FIX identifiers
    public string? SessionId { get; set; }
    public string ClOrdId { get; set; } = string.Empty;
    public string? OrigClOrdId { get; set; }
    public string? ExchangeOrderId { get; set; }

    // Instrument information
    public string Symbol { get; set; } = string.Empty;
    public int? OrderBook { get; set; }

    // Order details
    public string Side { get; set; } = string.Empty;
    public string? OrdType { get; set; }
    public decimal OrderQty { get; set; }
    public decimal? Price { get; set; }
    public string? TimeInForce { get; set; }

    // Current order status
    public string? OrdStatus { get; set; }
    public decimal? LeavesQty { get; set; }
    public decimal? CumQty { get; set; }
    public decimal? AvgPx { get; set; }

    // Account information
    public string? Account { get; set; }
    public int? AccountType { get; set; }
    public string? OrderSource { get; set; }
    public decimal? StopPx { get; set; }

    // Timestamps
    public DateTimeOffset? TransactTime { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public ICollection<Execution> Executions { get; set; }
    = new List<Execution>();

    public ICollection<OrderIdentifier> Identifiers { get; set; }
    = new List<OrderIdentifier>();
}