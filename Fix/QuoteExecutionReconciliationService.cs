
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Fix;

public class QuoteExecutionReconciliationService
{
    private readonly TradingDbContext _dbContext;
    private readonly ILogger<QuoteExecutionReconciliationService> _logger;

    public QuoteExecutionReconciliationService(
        TradingDbContext dbContext,
        ILogger<QuoteExecutionReconciliationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<int> ReconcilePendingAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        // Serialize quote execution reconciliation across application instances.
        // The transaction-level lock is automatically released on commit or rollback.
        await _dbContext.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(74021, 10);",
            cancellationToken);
            
        var pendingLegs = await _dbContext.Executions
            .Where(x =>
                x.MultiLegReportingType == "2" &&
                x.QuoteCorrelationStatus == "UNRESOLVED" &&
                x.QuoteEntryRevisionId == null &&
                x.SecondaryOrderId != null &&
                x.ClOrdId != null)
            .ToListAsync(cancellationToken);



        var reconciledLegs = new List<Execution>();

    // Collect distinct values needed for the parent lookup.
    var sessionIds = pendingLegs
        .Select(x => x.SessionId)
        .Distinct()
        .ToArray();

    var clOrdIds = pendingLegs
        .Select(x => x.ClOrdId!)
        .Distinct()
        .ToArray();

    var secondaryOrderIds = pendingLegs
        .Select(x => x.SecondaryOrderId!)
        .Distinct()
        .ToArray();

    // Retrieve candidate parent executions in one database query.
    var parentExecutions = await _dbContext.Executions
        .Where(x =>
            sessionIds.Contains(x.SessionId) &&
            clOrdIds.Contains(x.ClOrdId!) &&
            secondaryOrderIds.Contains(x.ExchangeOrderId!) &&
            x.MultiLegReportingType == "3" &&
            x.QuoteEntryRevisionId.HasValue)
        .Select(x => new
        {
            x.SessionId,
            x.ClOrdId,
            x.ExchangeOrderId,
            QuoteEntryRevisionId = x.QuoteEntryRevisionId!.Value
        })
        .ToListAsync(cancellationToken);

    // Group candidates by the exact parent-correlation key.
    var parentRevisionIdsByKey = parentExecutions
        .GroupBy(x => (
            SessionId: x.SessionId,
            ClOrdId: x.ClOrdId!,
            ExchangeOrderId: x.ExchangeOrderId!))
        .ToDictionary(
            group => group.Key,
            group => group
                .Select(x => x.QuoteEntryRevisionId)
                .Distinct()
                .ToList());

foreach (var leg in pendingLegs)
{
    var parentKey = (
        SessionId: leg.SessionId,
        ClOrdId: leg.ClOrdId!,
        ExchangeOrderId: leg.SecondaryOrderId!);

    if (!parentRevisionIdsByKey.TryGetValue(
            parentKey,
            out var parentRevisionIds))
    {
        continue;
    }

    if (parentRevisionIds.Count > 1)
    {
        _logger.LogWarning(
            "Quote leg reconciliation remains ambiguous. " +
            "SessionId={SessionId}, LegExecID={ExecId}, " +
            "ParentOrderID={ParentOrderId}",
            leg.SessionId,
            leg.ExecId,
            leg.SecondaryOrderId);

        leg.QuoteCorrelationStatus = "AMBIGUOUS";
        continue;
    }

    leg.QuoteEntryRevisionId = parentRevisionIds[0];
    leg.QuoteCorrelationStatus = "MATCHED";

    reconciledLegs.Add(leg);
}

        // Validate correction/cancellation references after assigning
        // the quote revision to every successfully reconciled leg.
        foreach (var leg in reconciledLegs)
        {
            if (leg.ExecType != "G" && leg.ExecType != "H")
            {
                continue;
            }

            Execution? referencedExecution = null;

            if (leg.ReferencedExecutionId.HasValue)
            {
                referencedExecution = await _dbContext.Executions
                    .FirstOrDefaultAsync(
                        x => x.Id == leg.ReferencedExecutionId.Value,
                        cancellationToken);
            }

            // A reference may have arrived after this leg was persisted.
            if (referencedExecution == null &&
                !string.IsNullOrWhiteSpace(leg.ExecRefId))
            {
                referencedExecution = await _dbContext.Executions
                    .FirstOrDefaultAsync(
                        x => x.SessionId == leg.SessionId &&
                             x.ExecId == leg.ExecRefId,
                        cancellationToken);
            }

            if (referencedExecution == null)
            {
                _logger.LogWarning(
                    "Reconciled quote leg correction/cancellation " +
                    "has no available referenced execution. " +
                    "SessionId={SessionId}, ExecID={ExecId}, " +
                    "ExecRefID={ExecRefId}",
                    leg.SessionId,
                    leg.ExecId,
                    leg.ExecRefId);

                leg.ReferencedExecutionId = null;
                leg.ReferencedExecution = null;
                continue;
            }

            if (referencedExecution.QuoteEntryRevisionId !=
                leg.QuoteEntryRevisionId)
            {
                _logger.LogError(
                    "Reconciled quote leg correction/cancellation " +
                    "references a different or unresolved quote revision. " +
                    "SessionId={SessionId}, ExecID={ExecId}, " +
                    "ExecRefID={ExecRefId}, QuoteRevisionId={QuoteRevisionId}, " +
                    "ReferencedRevisionId={ReferencedRevisionId}",
                    leg.SessionId,
                    leg.ExecId,
                    leg.ExecRefId,
                    leg.QuoteEntryRevisionId,
                    referencedExecution.QuoteEntryRevisionId);

                leg.ReferencedExecutionId = null;
                leg.ReferencedExecution = null;
                continue;
            }

            leg.ReferencedExecutionId = referencedExecution.Id;
            leg.ReferencedExecution = referencedExecution;
        }

        // Retry references for quote corrections/cancellations that were
// previously matched but whose original execution was not available.
var pendingReferenceExecutions = await _dbContext.Executions
    .Where(x =>
        x.MultiLegReportingType == "2" &&
        (x.ExecType == "G" || x.ExecType == "H") &&
        x.QuoteCorrelationStatus == "MATCHED" &&
        x.QuoteEntryRevisionId.HasValue &&
        x.ReferencedExecutionId == null &&
        x.ExecRefId != null)
    .ToListAsync(cancellationToken);

if (pendingReferenceExecutions.Count > 0)
{
    var referenceSessionIds = pendingReferenceExecutions
        .Select(x => x.SessionId)
        .Distinct()
        .ToArray();

    var referenceExecIds = pendingReferenceExecutions
        .Select(x => x.ExecRefId!)
        .Distinct()
        .ToArray();

    var originalExecutions = await _dbContext.Executions
        .Where(x =>
            referenceSessionIds.Contains(x.SessionId) &&
            referenceExecIds.Contains(x.ExecId))
        .ToListAsync(cancellationToken);

    var originalExecutionLookup = originalExecutions
        .ToDictionary(x => (x.SessionId, x.ExecId));

    foreach (var execution in pendingReferenceExecutions)
    {
        if (string.IsNullOrWhiteSpace(execution.ExecRefId))
        {
            continue;
        }

        if (!originalExecutionLookup.TryGetValue(
                (execution.SessionId, execution.ExecRefId),
                out var originalExecution))
        {
            // Original execution has not arrived yet.
            // Leave it pending for the next reconciliation cycle.
            continue;
        }

        if (!originalExecution.QuoteEntryRevisionId.HasValue)
        {
            // Original execution has not yet been correlated to a quote.
            continue;
        }

        if (originalExecution.QuoteEntryRevisionId !=
            execution.QuoteEntryRevisionId)
        {
            _logger.LogError(
                "Late quote execution reference belongs to a different " +
                "quote revision. SessionId={SessionId}, ExecID={ExecId}, " +
                "ExecRefID={ExecRefId}, ExecutionRevisionId={ExecutionRevisionId}, " +
                "OriginalRevisionId={OriginalRevisionId}",
                execution.SessionId,
                execution.ExecId,
                execution.ExecRefId,
                execution.QuoteEntryRevisionId,
                originalExecution.QuoteEntryRevisionId);

            continue;
        }

        execution.ReferencedExecutionId = originalExecution.Id;
        execution.ReferencedExecution = originalExecution;

        _logger.LogInformation(
            "Late quote execution reference resolved. SessionId={SessionId}, " +
            "ExecID={ExecId}, ExecRefID={ExecRefId}",
            execution.SessionId,
            execution.ExecId,
            execution.ExecRefId);
    }
}

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Quote execution reconciliation completed. " +
            "PendingLegCount={PendingCount}, ReconciledCount={ReconciledCount}",
            pendingLegs.Count,
            reconciledLegs.Count);

        return reconciledLegs.Count;
    }
}
