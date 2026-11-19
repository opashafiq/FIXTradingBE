namespace TradingBackend.Api.Fix.Models;

public class EffectiveTradeCalculationResult
{
    public IReadOnlyList<EffectiveTrade> Trades { get; set; }
        = Array.Empty<EffectiveTrade>();

    public IReadOnlyList<ExecutionReferenceIssue> Issues { get; set; }
        = Array.Empty<ExecutionReferenceIssue>();
}