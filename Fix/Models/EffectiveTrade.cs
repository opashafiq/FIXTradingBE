
namespace TradingBackend.Api.Fix.Models;

public class EffectiveTrade
{
    // Original execution event
    public long OriginalExecutionId { get; set; }

    public string OriginalExecId { get; set; } = string.Empty;

    public string SessionId { get; set; } = string.Empty;

    // Associated order or quote
    public long? TradingOrderId { get; set; }

    public long? QuoteEntryRevisionId { get; set; }

    // CHSE multi-leg classification
    public string? MultiLegReportingType { get; set; }

    // Trade information
    public string? Symbol { get; set; }

    public string? Side { get; set; }

    public int? OrderBook { get; set; }

    // Calculated effective values
    public decimal? EffectiveQty { get; set; }

    public decimal? EffectivePrice { get; set; }

    public EffectiveTradeStatus Status { get; set; }

    // Event determining the current effective result
    public long EffectiveExecutionId { get; set; }

    public string EffectiveExecId { get; set; } = string.Empty;

    // Original and associated correction/cancellation event IDs
    public List<long> ExecutionEventIds { get; set; } = new();
}
