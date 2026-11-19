
namespace TradingBackend.Api.Fix.Models;

public class ExecutionReferenceIssue
{
    public long ExecutionId { get; set; }

    public string ExecId { get; set; } = string.Empty;

    public string? ExecRefId { get; set; }

    public string SessionId { get; set; } = string.Empty;

    public ExecutionReferenceIssueType IssueType { get; set; }

    public string Description { get; set; } = string.Empty;
}
