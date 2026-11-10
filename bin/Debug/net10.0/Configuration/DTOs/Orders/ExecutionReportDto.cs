namespace TradingBackend.Api.DTOs.Orders;

public class ExecutionReportDto
{
    public string SessionId { get; set; } = string.Empty;

    public string? ApplId { get; set; }
    public string? ExecId { get; set; }
    public string? ClOrdId { get; set; }
    public string? OrigClOrdId { get; set; }
    public string? OrderId { get; set; }

    public string? ExecType { get; set; }
    public string? OrdStatus { get; set; }

    public string? Symbol { get; set; }
    public string? Side { get; set; }

    public decimal? OrderQty { get; set; }
    public decimal? LastQty { get; set; }
    public decimal? LastPx { get; set; }

    public decimal? LeavesQty { get; set; }
    public decimal? CumQty { get; set; }
    public decimal? AvgPx { get; set; }

    public int? OrderBook { get; set; }
    public string? OrderSource { get; set; }

    public DateTimeOffset? TransactTime { get; set; }

    public string? ExecRefId { get; set; }
    public string? Text { get; set; }


    public string? MdEntryId { get; set; }
    public string? OrderCapacity { get; set; }

    public string? TrdMatchId { get; set; }
    public string? SecondaryTradeId { get; set; }
    public string? ExecRestatementReason { get; set; }
    public string? OrdRejReason { get; set; }

    public string? OrdType { get; set; }

    public string? Account { get; set; }
    public int? AccountType { get; set; }

    public decimal? Yield { get; set; }
    public decimal? ConvertedYield { get; set; }

    public string? StreamText { get; set; }
}