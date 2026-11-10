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

    public string? MdEntryId { get; set; }
    public string? OrderCapacity { get; set; }

    public string? TrdMatchId { get; set; }
    public string? SecondaryTradeId { get; set; }

    public string? ExecRestatementReason { get; set; }
    public string? OrdRejReason { get; set; }

    public string EventCategory { get; set; } = "OTHER";

    public long? ReferencedExecutionId { get; set; }

    public Execution? ReferencedExecution { get; set; }

    public ICollection<Execution> RelatedExecutions { get; set; }
        = new List<Execution>();
}