namespace TradingBackend.Api.Entities;

public class Execution
{
    public long Id { get; set; }

    // FIX identifiers
    public string ExecId { get; set; } = string.Empty;
    public string? ExecRefId { get; set; }
    public string? ClOrdId { get; set; }
    public string? ExchangeOrderId { get; set; }
    public string SessionId { get; set; } = string.Empty;

    // Execution information
    public string ExecType { get; set; } = string.Empty;
    public string OrdStatus { get; set; } = string.Empty;

    public decimal? LastQty { get; set; }
    public decimal? LastPx { get; set; }

    public decimal? CumQty { get; set; }
    public decimal? LeavesQty { get; set; }
    public decimal? AvgPx { get; set; }

    public string? Symbol { get; set; }
    public string? Side { get; set; }

    public int? OrderBook { get; set; }

    public DateTimeOffset? TransactTime { get; set; }

    // Relationship with TradingOrder
    public long? TradingOrderId { get; set; }

    public TradingOrder? TradingOrder { get; set; }

    // Audit
    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;
}