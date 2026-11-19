namespace TradingBackend.Api.Fix.Models;

public enum ExecutionReferenceIssueType
{
    OriginalNotFound = 1,
    OriginalIsNotTrade = 2,
    InvalidReference = 3,
    CrossRevisionReference = 4,
    ConflictingSuccessors = 5,
    MissingTradeValues = 6,
    UnresolvedCorrelation = 7
}