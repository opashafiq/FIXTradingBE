namespace TradingBackend.Api.Fix.Models;

public enum EffectiveTradeStatus
{
    Active = 1,
    Cancelled = 2,
    Ambiguous = 3,
    Unresolved = 4,
    Invalid = 5
}