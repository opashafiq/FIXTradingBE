
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Orders;
using TradingBackend.Api.Entities;
using Npgsql;

namespace TradingBackend.Api.Fix;

public class ExecutionReportPersistenceService
{
    private readonly TradingDbContext _dbContext;
    private readonly ILogger<ExecutionReportPersistenceService> _logger;
    private readonly QuoteExecutionReconciliationService _quoteExecutionReconciliationService;
    public ExecutionReportPersistenceService(
        TradingDbContext dbContext,
        ILogger<ExecutionReportPersistenceService> logger,
        QuoteExecutionReconciliationService quoteExecutionReconciliationService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _quoteExecutionReconciliationService =
            quoteExecutionReconciliationService;
    }



private async Task<QuoteCorrelationResult> CorrelateQuoteExecutionAsync(
    ExecutionReportDto report,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(report.ClOrdId))
    {
        return new QuoteCorrelationResult();
    }


    // Individual leg execution of a multi-leg quote.
    // CHSE SecondaryOrderID (198) identifies the strategy-side
    // OrderID (37), not the individual leg OrderID.
    if (report.MultiLegReportingType == "2")
    {
        if (string.IsNullOrWhiteSpace(report.SecondaryOrderId))
        {
            return new QuoteCorrelationResult
            {
                Status = "UNRESOLVED"
            };
        }

        // Find the previously persisted overall strategy execution.
        var parentQuoteRevisionIds = await _dbContext.Executions
            .Where(x =>
                x.SessionId == report.SessionId &&
                x.ClOrdId == report.ClOrdId &&
                x.MultiLegReportingType == "3" &&
                x.ExchangeOrderId == report.SecondaryOrderId &&
                x.QuoteEntryRevisionId.HasValue)
            .Select(x => x.QuoteEntryRevisionId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (parentQuoteRevisionIds.Count == 1)
        {
            return new QuoteCorrelationResult
            {
                Status = "MATCHED",
                QuoteEntryRevisionId = parentQuoteRevisionIds[0]
            };
        }

        if (parentQuoteRevisionIds.Count > 1)
        {
            return new QuoteCorrelationResult
            {
                Status = "AMBIGUOUS"
            };
        }

        // The parent execution may not have arrived yet.
        // Determine whether this report belongs to a known quote.
        bool singleQuoteExists =
            await _dbContext.QuoteSubmissions.AnyAsync(
                x => x.SessionId == report.SessionId &&
                    x.MsgType == "S" &&
                    x.QuoteMsgID == report.ClOrdId,
                cancellationToken);

        bool massQuoteExists =
            await _dbContext.QuoteSubmissions.AnyAsync(
                x => x.SessionId == report.SessionId &&
                    x.MsgType == "i" &&
                    x.QuoteID == report.ClOrdId,
                cancellationToken);

        bool massQuoteEntryExists = await (
            from submission in _dbContext.QuoteSubmissions
            join revision in _dbContext.QuoteEntryRevisions
                on submission.Id equals revision.QuoteSubmissionId
            where submission.SessionId == report.SessionId
                && submission.MsgType == "i"
                && revision.QuoteEntryID == report.ClOrdId
            select revision.Id
        ).AnyAsync(cancellationToken);

        if (singleQuoteExists ||
            massQuoteExists ||
            massQuoteEntryExists)
        {
            return new QuoteCorrelationResult
            {
                Status = "UNRESOLVED"
            };
        }

        return new QuoteCorrelationResult();
    }


    // Single Quote correlation:
    // ClOrdID corresponds to QuoteMsgID.
    var singleQuoteCandidates = await (
        from submission in _dbContext.QuoteSubmissions
        join revision in _dbContext.QuoteEntryRevisions
            on submission.Id equals revision.QuoteSubmissionId
        where submission.SessionId == report.SessionId
              && submission.MsgType == "S"
              && submission.QuoteMsgID == report.ClOrdId
        select new
        {
            RevisionId = revision.Id,
            revision.Symbol
        })
        .ToListAsync(cancellationToken);

    var matchingSingleQuotes = singleQuoteCandidates
        .Where(x =>
            !string.IsNullOrWhiteSpace(report.Symbol) &&
            x.Symbol == report.Symbol)
        .ToList();

    if (matchingSingleQuotes.Count == 1)
    {
        return new QuoteCorrelationResult
        {
            Status = "MATCHED",
            QuoteEntryRevisionId = matchingSingleQuotes[0].RevisionId
        };
    }

    if (matchingSingleQuotes.Count > 1)
    {
        return new QuoteCorrelationResult
        {
            Status = "AMBIGUOUS"
        };
    }

    // Check whether a Single Quote submission exists
    // even if its revision cannot be located.
    bool singleQuoteSubmissionExists =
        await _dbContext.QuoteSubmissions.AnyAsync(
            x => x.SessionId == report.SessionId &&
                 x.MsgType == "S" &&
                 x.QuoteMsgID == report.ClOrdId,
            cancellationToken);

    if (singleQuoteSubmissionExists)
    {
        return new QuoteCorrelationResult
        {
            Status = "UNRESOLVED"
        };
    }

    // Mass Quote correlation.
    // CHSE documentation has conflicting descriptions of
    // the ClOrdID value, so neither identifier is sufficient
    // to establish a confirmed match at this stage.
    var massQuoteCandidates = await (
        from submission in _dbContext.QuoteSubmissions
        join revision in _dbContext.QuoteEntryRevisions
            on submission.Id equals revision.QuoteSubmissionId
        where submission.SessionId == report.SessionId
              && submission.MsgType == "i"
              && (
                  submission.QuoteID == report.ClOrdId ||
                  revision.QuoteEntryID == report.ClOrdId
              )
        select new
        {
            RevisionId = revision.Id,
            revision.Symbol
        })
        .ToListAsync(cancellationToken);

    var matchingMassQuotes = massQuoteCandidates
        .Where(x =>
            !string.IsNullOrWhiteSpace(report.Symbol) &&
            x.Symbol == report.Symbol)
        .ToList();

    if (matchingMassQuotes.Count > 1)
    {
        return new QuoteCorrelationResult
        {
            Status = "AMBIGUOUS"
        };
    }

    if (matchingMassQuotes.Count == 1)
    {
        return new QuoteCorrelationResult
        {
            Status = "UNRESOLVED"
        };
    }

    // Also recognize a Mass Quote submission that has no
    // corresponding revision record.
    bool massQuoteSubmissionExists =
        await _dbContext.QuoteSubmissions.AnyAsync(
            x => x.SessionId == report.SessionId &&
                 x.MsgType == "i" &&
                 x.QuoteID == report.ClOrdId,
            cancellationToken);

    return massQuoteSubmissionExists
        ? new QuoteCorrelationResult
        {
            Status = "UNRESOLVED"
        }
        : new QuoteCorrelationResult();
}

    private async Task TryReconcileQuoteExecutionsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            int reconciledCount =
                await _quoteExecutionReconciliationService
                    .ReconcilePendingAsync(cancellationToken);

            if (reconciledCount > 0)
            {
                _logger.LogInformation(
                    "Post-persistence quote execution reconciliation completed. " +
                    "ReconciledCount={ReconciledCount}",
                    reconciledCount);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The ExecutionReport is already committed.
            // Reconciliation failure must not undo or misreport it.
            _logger.LogError(
                ex,
                "Post-persistence quote execution reconciliation failed. " +
                "The reconciliation can be retried later.");
        }
    }
public async Task PersistAsync(
    ExecutionReportDto report,
    long logId,
    Guid processingToken,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(report.ExecId))
    {
        throw new InvalidOperationException(
            "ExecutionReport is missing ExecID (tag 17).");
    }

    // Start transaction before verifying processing ownership.
    await using var transaction =
        await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

    // Verify ownership and refresh the processing timestamp.
    // This conditional update also locks the message-log row
    // until the transaction completes.
    var now = DateTimeOffset.UtcNow;

    int ownershipConfirmed = await _dbContext.FixMessageLogs
        .Where(x =>
            x.Id == logId &&
            x.ProcessingStatus == "PROCESSING" &&
            x.ProcessingToken == processingToken)
        .ExecuteUpdateAsync(setters => setters
            .SetProperty(
                x => x.ProcessingStartedAtUtc,
                now),
            cancellationToken);

    if (ownershipConfirmed == 0)
    {
        throw new InvalidOperationException(
            $"FIX message processing ownership lost. LogId: {logId}");
    }


    // Confirm the matching cancellation request when the exchange
    // reports an actual cancellation event.
    if (report.ExecType == "4" &&
        report.OrdStatus == "4" &&
        !string.IsNullOrWhiteSpace(report.ClOrdId))
    {
        var cancellationRequest =
            await _dbContext.OrderCancellationRequests
                .FirstOrDefaultAsync(x =>
                    x.SessionId == report.SessionId &&
                    x.ClOrdId == report.ClOrdId,
                    cancellationToken);

        if (cancellationRequest != null)
        {
            if (cancellationRequest.Status == "PendingSubmission" ||
                cancellationRequest.Status == "SubmittedToFix" ||
                cancellationRequest.Status == "SubmissionUnknown")
            {
                cancellationRequest.Status = "Confirmed";
                cancellationRequest.SubmissionError = null;
                cancellationRequest.UpdatedAtUtc = DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "Cancellation request confirmed by ExecutionReport. " +
                    "TradingOrderId={TradingOrderId}, " +
                    "CancellationRequestId={CancellationRequestId}, " +
                    "ClOrdID={ClOrdId}, ExecID={ExecId}",
                    cancellationRequest.TradingOrderId,
                    cancellationRequest.Id,
                    cancellationRequest.ClOrdId,
                    report.ExecId);
            }
        }
        else
        {
            _logger.LogWarning(
                "Cancellation ExecutionReport received, but no matching " +
                "cancellation request was found. SessionId={SessionId}, " +
                "ClOrdID={ClOrdId}",
                report.SessionId,
                report.ClOrdId);
        }
    }



    // Reconcile replacement responses before duplicate ExecID detection.
    if ((report.ExecType == "E" || report.ExecType == "5") &&
        !string.IsNullOrWhiteSpace(report.ClOrdId))
    {
        var replacementRequest =
            await _dbContext.OrderReplacementRequests
                .FirstOrDefaultAsync(
                    x => x.SessionId == report.SessionId &&
                         x.ClOrdId == report.ClOrdId,
                    cancellationToken);

        if (replacementRequest == null)
        {
            _logger.LogWarning(
                "Replacement ExecutionReport received, but no matching " +
                "replacement request was found. SessionId={SessionId}, " +
                "ClOrdID={ClOrdId}, ExecType={ExecType}",
                report.SessionId,
                report.ClOrdId,
                report.ExecType);
        }
        else
        {
            var replacementOrder = await _dbContext.Orders
                .FirstOrDefaultAsync(
                    x => x.Id == replacementRequest.TradingOrderId,
                    cancellationToken);

            if (report.ExecType == "E")
            {
                // Exchange acknowledges that replacement is pending.
                if (replacementRequest.Status == "PendingSubmission" ||
                    replacementRequest.Status == "SubmittedToFix" ||
                    replacementRequest.Status == "SubmissionUnknown")
                {
                    replacementRequest.Status = "PendingReplace";
                    replacementRequest.UpdatedAtUtc =
                        DateTimeOffset.UtcNow;
                }

                _logger.LogInformation(
                    "Replacement is pending at the exchange. " +
                    "TradingOrderId={TradingOrderId}, " +
                    "ReplacementRequestId={RequestId}, ClOrdID={ClOrdId}",
                    replacementRequest.TradingOrderId,
                    replacementRequest.Id,
                    replacementRequest.ClOrdId);
            }
            else if (report.ExecType == "5")
            {
                // Exchange confirms that replacement was completed.
                if (replacementRequest.Status == "PendingSubmission" ||
                    replacementRequest.Status == "SubmittedToFix" ||
                    replacementRequest.Status == "SubmissionUnknown" ||
                    replacementRequest.Status == "PendingReplace")
                {
                    replacementRequest.Status = "Confirmed";
                    replacementRequest.SubmissionError = null;
                    replacementRequest.UpdatedAtUtc =
                        DateTimeOffset.UtcNow;
                }

                bool replacementCanBeApplied =
                replacementRequest.Status == "Confirmed" ||
                replacementRequest.Status == "PendingSubmission" ||
                replacementRequest.Status == "SubmittedToFix" ||
                replacementRequest.Status == "SubmissionUnknown" ||
                replacementRequest.Status == "PendingReplace";

                if (replacementOrder != null && replacementCanBeApplied)
                {
                    bool canApplyReplacement =
                        report.TransactTime.HasValue &&
                        (!replacementOrder.TransactTime.HasValue ||
                        report.TransactTime.Value >=
                        replacementOrder.TransactTime.Value);

                    if (canApplyReplacement)
                    {
                        // Apply the exchange-confirmed replacement terms.
                        replacementOrder.ClOrdId =
                            replacementRequest.ClOrdId;

                        replacementOrder.OrderQty =
                            replacementRequest.OrderQty;

                        replacementOrder.Price =
                            replacementRequest.Price;

                        replacementOrder.StopPx =
                            replacementRequest.StopPx;

                        replacementOrder.TimeInForce =
                            replacementRequest.TimeInForce;

                        replacementOrder.ExpireDate =
                            replacementRequest.ExpireDate;

                        replacementOrder.ExchangeOrderId =
                            report.OrderId ??
                            replacementOrder.ExchangeOrderId;

                        replacementOrder.OrdStatus =
                            report.OrdStatus ??
                            replacementOrder.OrdStatus;

                        replacementOrder.LeavesQty =
                            report.LeavesQty ??
                            replacementOrder.LeavesQty;

                        replacementOrder.CumQty =
                            report.CumQty ??
                            replacementOrder.CumQty;

                        replacementOrder.AvgPx =
                            report.AvgPx ??
                            replacementOrder.AvgPx;

                        replacementOrder.TransactTime =
                            report.TransactTime;

                        replacementOrder.UpdatedAtUtc =
                            DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Replacement confirmed, but order terms were not updated " +
                            "because a newer order event already exists or the report " +
                            "has no TransactTime. TradingOrderId={TradingOrderId}",
                            replacementOrder.Id);
                    }
                }
                else if (replacementOrder != null)
                {
                    _logger.LogError(
                        "Replacement confirmation received for a replacement request " +
                        "that is already rejected or otherwise inactive. " +
                        "Manual reconciliation may be required. " +
                        "TradingOrderId={TradingOrderId}, " +
                        "ReplacementRequestId={ReplacementRequestId}, Status={Status}",
                        replacementOrder.Id,
                        replacementRequest.Id,
                        replacementRequest.Status);
                }

                _logger.LogInformation(
                    "Replacement confirmed by exchange. " +
                    "TradingOrderId={TradingOrderId}, " +
                    "ReplacementRequestId={RequestId}, ClOrdID={ClOrdId}",
                    replacementRequest.TradingOrderId,
                    replacementRequest.Id,
                    replacementRequest.ClOrdId);
            }
        }
    }



    // Prevent duplicate ExecutionReports.
    bool alreadyExists = await _dbContext.Executions.AnyAsync(
        x => x.SessionId == report.SessionId &&
             x.ExecId == report.ExecId,
        cancellationToken);

    if (alreadyExists)
    {
        _logger.LogInformation(
            "Duplicate ExecutionReport ignored. Session: {SessionId}, ExecID: {ExecId}",
            report.SessionId,
            report.ExecId);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await TryReconcileQuoteExecutionsAsync(cancellationToken);

        return;
    }
    var order = await _dbContext.OrderIdentifiers
        .Where(x =>
            x.SessionId == report.SessionId &&
            (
                x.ClOrdId == report.ClOrdId ||
                (
                    !string.IsNullOrWhiteSpace(report.OrigClOrdId) &&
                    x.ClOrdId == report.OrigClOrdId
                )
            ))
        .OrderBy(x => x.ClOrdId == report.ClOrdId ? 0 : 1)
        .Select(x => x.TradingOrder)
        .FirstOrDefaultAsync(cancellationToken);

    // If this is not an order execution, attempt quote correlation.
    QuoteCorrelationResult quoteCorrelation = order == null
    ? await CorrelateQuoteExecutionAsync(report, cancellationToken)
    : new QuoteCorrelationResult();    



    Execution? referencedExecution = null;

    if (!string.IsNullOrWhiteSpace(report.ExecRefId))
    {
        referencedExecution = await _dbContext.Executions
            .FirstOrDefaultAsync(
                x => x.SessionId == report.SessionId &&
                     x.ExecId == report.ExecRefId,
                cancellationToken);

        if (referencedExecution == null)
        {
            _logger.LogWarning(
                "Referenced execution not found. SessionId={SessionId}, ExecRefID={ExecRefId}, ExecID={ExecId}",
                report.SessionId,
                report.ExecRefId,
                report.ExecId);
        }
    }

    var execution = new Execution
    {
        SessionId = report.SessionId,
        ExecId = report.ExecId,
        ExecRefId = report.ExecRefId,
        ReferencedExecutionId = referencedExecution?.Id,
        ClOrdId = report.ClOrdId,
        ExchangeOrderId = report.OrderId,
        ExecType = report.ExecType ?? string.Empty,
        EventCategory = GetExecutionCategory(report.ExecType),
        OrdStatus = report.OrdStatus ?? string.Empty,
        LastQty = report.LastQty,
        LastPx = report.LastPx,
        CumQty = report.CumQty,
        LeavesQty = report.LeavesQty,
        AvgPx = report.AvgPx,
        Symbol = report.Symbol,
        Side = report.Side,
        OrderBook = report.OrderBook,
        TransactTime = report.TransactTime,
        TradingOrderId = order?.Id,
        // Quote execution correlation
        QuoteEntryRevisionId = quoteCorrelation.QuoteEntryRevisionId,
        QuoteCorrelationStatus = quoteCorrelation.Status,

        // Additional CHSE execution identifiers
        ApplId = report.ApplId,
        MultiLegReportingType = report.MultiLegReportingType,
        SecondaryOrderId = report.SecondaryOrderId,
        MdEntryId = report.MdEntryId,
        OrderCapacity = report.OrderCapacity,
        TrdMatchId = report.TrdMatchId,
        SecondaryTradeId = report.SecondaryTradeId,
        ExecRestatementReason = report.ExecRestatementReason,
        OrdRejReason = report.OrdRejReason,
    };

    // Validate the execution reference for quote corrections and cancellations.
    if (quoteCorrelation.QuoteEntryRevisionId.HasValue &&
        (report.ExecType == "G" || report.ExecType == "H"))
    {
        if (referencedExecution == null)
        {
            _logger.LogWarning(
                "Quote correction/cancellation references an execution " +
                "that has not been found. SessionId={SessionId}, " +
                "ExecRefID={ExecRefId}, ExecID={ExecId}",
                report.SessionId,
                report.ExecRefId,
                report.ExecId);
        }
        else if (referencedExecution.QuoteEntryRevisionId !=
                quoteCorrelation.QuoteEntryRevisionId)
        {
            _logger.LogError(
                "Quote correction/cancellation references an execution " +
                "belonging to a different quote revision. " +
                "SessionId={SessionId}, ExecRefID={ExecRefId}, " +
                "ExecID={ExecId}, QuoteEntryRevisionId={QuoteEntryRevisionId}, " +
                "ReferencedQuoteEntryRevisionId={ReferencedQuoteEntryRevisionId}",
                report.SessionId,
                report.ExecRefId,
                report.ExecId,
                quoteCorrelation.QuoteEntryRevisionId,
                referencedExecution.QuoteEntryRevisionId);

            // Do not create an invalid cross-quote execution relationship.
            referencedExecution = null;
            execution.ReferencedExecutionId = null;
            execution.ReferencedExecution = null;
        }
    }

    _dbContext.Executions.Add(execution);

    // Reconcile individual leg executions that arrived before
    // their overall multi-leg strategy execution.
    if (report.MultiLegReportingType == "3" &&
        quoteCorrelation.QuoteEntryRevisionId.HasValue &&
        !string.IsNullOrWhiteSpace(report.OrderId) &&
        !string.IsNullOrWhiteSpace(report.ClOrdId))
    {
        await _dbContext.Database.ExecuteSqlRawAsync(
        "SELECT pg_advisory_xact_lock(74021, 10);",
        cancellationToken);

        var unresolvedLegExecutions = await _dbContext.Executions
            .Where(x =>
                x.SessionId == report.SessionId &&
                x.ClOrdId == report.ClOrdId &&
                x.MultiLegReportingType == "2" &&
                x.SecondaryOrderId == report.OrderId &&
                x.QuoteEntryRevisionId == null &&
                x.QuoteCorrelationStatus == "UNRESOLVED")
            .ToListAsync(cancellationToken);


var unresolvedLegExecutionIds = unresolvedLegExecutions
    .Select(x => x.Id)
    .ToHashSet();

// First assign the parent quote revision to all matching legs.
// This allows references between two legs in this batch
// to be validated consistently.
foreach (var legExecution in unresolvedLegExecutions)
{
    legExecution.QuoteEntryRevisionId =
        quoteCorrelation.QuoteEntryRevisionId;

    legExecution.QuoteCorrelationStatus = "MATCHED";
}

// Load references that are outside the reconciliation batch.
var externalReferenceIds = unresolvedLegExecutions
    .Where(x => x.ReferencedExecutionId.HasValue)
    .Select(x => x.ReferencedExecutionId!.Value)
    .Where(x => !unresolvedLegExecutionIds.Contains(x))
    .Distinct()
    .ToList();

var externalReferences = await _dbContext.Executions
    .Where(x => externalReferenceIds.Contains(x.Id))
    .Select(x => new
    {
        x.Id,
        x.QuoteEntryRevisionId
    })
    .ToDictionaryAsync(
        x => x.Id,
        x => x.QuoteEntryRevisionId,
        cancellationToken);

    // Validate each leg correction/cancellation reference.
    foreach (var legExecution in unresolvedLegExecutions)
    {
        if (legExecution.ExecType != "G" &&
            legExecution.ExecType != "H")
        {
            continue;
        }

if (!legExecution.ReferencedExecutionId.HasValue &&
    !string.IsNullOrWhiteSpace(legExecution.ExecRefId))
{
    var sameBatchReference = unresolvedLegExecutions
        .FirstOrDefault(candidate =>
            candidate.Id != legExecution.Id &&
            candidate.SessionId == legExecution.SessionId &&
            candidate.ExecId == legExecution.ExecRefId);

    if (sameBatchReference != null)
    {
        legExecution.ReferencedExecutionId = sameBatchReference.Id;
        legExecution.ReferencedExecution = sameBatchReference;
    }
}

if (!legExecution.ReferencedExecutionId.HasValue)
{
    _logger.LogWarning(
        "Reconciled quote leg correction/cancellation has no " +
        "resolved execution reference. SessionId={SessionId}, " +
        "ExecID={ExecId}, ExecRefID={ExecRefId}",
        legExecution.SessionId,
        legExecution.ExecId,
        legExecution.ExecRefId);

    continue;
}

        long? referencedRevisionId = null;

        if (unresolvedLegExecutionIds.Contains(
            legExecution.ReferencedExecutionId.Value))
        {
            // The referenced leg was reconciled in this same batch.
            referencedRevisionId =
                quoteCorrelation.QuoteEntryRevisionId;
        }
        else if (externalReferences.TryGetValue(
            legExecution.ReferencedExecutionId.Value,
            out var externalRevisionId))
        {
            referencedRevisionId = externalRevisionId;
        }

        if (referencedRevisionId !=
            quoteCorrelation.QuoteEntryRevisionId)
        {
            _logger.LogError(
                "Reconciled quote leg correction/cancellation references " +
                "an execution belonging to a different or unresolved " +
                "quote revision. SessionId={SessionId}, ExecID={ExecId}, " +
                "ExecRefID={ExecRefId}, QuoteEntryRevisionId={QuoteRevisionId}, " +
                "ReferencedQuoteEntryRevisionId={ReferencedRevisionId}",
                legExecution.SessionId,
                legExecution.ExecId,
                legExecution.ExecRefId,
                quoteCorrelation.QuoteEntryRevisionId,
                referencedRevisionId);

            // Preserve ExecRefID, but remove an unverified relationship.
            legExecution.ReferencedExecutionId = null;
            legExecution.ReferencedExecution = null;
        }
    }


        foreach (var legExecution in unresolvedLegExecutions)
        {
            legExecution.QuoteEntryRevisionId =
                quoteCorrelation.QuoteEntryRevisionId;

            legExecution.QuoteCorrelationStatus = "MATCHED";

            _logger.LogInformation(
                "Previously unresolved quote leg execution reconciled. " +
                "SessionId={SessionId}, LegExecID={LegExecId}, " +
                "ParentOrderID={ParentOrderId}, " +
                "QuoteEntryRevisionId={QuoteEntryRevisionId}",
                legExecution.SessionId,
                legExecution.ExecId,
                report.OrderId,
                quoteCorrelation.QuoteEntryRevisionId);
        }
    }

    // Register a new client order identifier if necessary.
    if (order != null &&
        !string.IsNullOrWhiteSpace(report.ClOrdId))
    {
        bool identifierExists = await _dbContext.OrderIdentifiers
            .AnyAsync(
                x => x.SessionId == report.SessionId &&
                     x.ClOrdId == report.ClOrdId,
                cancellationToken);

        if (!identifierExists)
        {
            var newIdentifier = new OrderIdentifier
            {
                TradingOrderId = order.Id,
                SessionId = report.SessionId,
                ClOrdId = report.ClOrdId,
                OrigClOrdId = report.OrigClOrdId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            _dbContext.OrderIdentifiers.Add(newIdentifier);
        }
    }

    // Update the associated trading order.
    if (order != null)
    {
        bool canUpdateOrder =
            report.TransactTime.HasValue &&
            (!order.TransactTime.HasValue ||
             report.TransactTime.Value >= order.TransactTime.Value);

        if (canUpdateOrder)
        {
            if (report.ExecType != "E" && report.ExecType != "4")
            {
                order.ClOrdId = report.ClOrdId ?? order.ClOrdId;
            }
            order.ExchangeOrderId =
                report.OrderId ?? order.ExchangeOrderId;
            order.OrdStatus = report.OrdStatus ?? order.OrdStatus;
            order.LeavesQty = report.LeavesQty ?? order.LeavesQty;
            order.CumQty = report.CumQty ?? order.CumQty;
            order.AvgPx = report.AvgPx ?? order.AvgPx;
            order.TransactTime = report.TransactTime;
            order.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            _logger.LogInformation(
                "Skipping order update because the ExecutionReport is older. " +
                "ClOrdID={ClOrdId}, ReportTime={ReportTime}, CurrentOrderTime={OrderTime}",
                report.ClOrdId,
                report.TransactTime,
                order.TransactTime);
        }
    }
    else if (quoteCorrelation.Status != null)
    {
        _logger.LogInformation(
            "ExecutionReport classified as a quote execution. " +
            "SessionId={SessionId}, ClOrdID={ClOrdId}, " +
            "QuoteCorrelationStatus={QuoteCorrelationStatus}, " +
            "QuoteEntryRevisionId={QuoteEntryRevisionId}",
            report.SessionId,
            report.ClOrdId,
            quoteCorrelation.Status,
            quoteCorrelation.QuoteEntryRevisionId);
    }
    else
    {
        _logger.LogWarning(
            "No matching order or quote found for ExecutionReport. " +
            "SessionId={SessionId}, ClOrdID={ClOrdId}",
            report.SessionId,
            report.ClOrdId);
    }

    try
    {
        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        await TryReconcileQuoteExecutionsAsync(cancellationToken);
    }
    catch (DbUpdateException ex)
        when (IsDuplicateExecutionException(ex))
    {
        await transaction.RollbackAsync(cancellationToken);

        _logger.LogInformation(
            "Duplicate ExecutionReport detected during concurrent processing. " +
            "SessionId={SessionId}, ExecID={ExecId}",
            report.SessionId,
            report.ExecId);

        return;
    }

    _logger.LogInformation(
        "ExecutionReport persisted. ExecID: {ExecId}, OrderId: {OrderId}",
        report.ExecId,
        order?.Id);
}


private sealed class QuoteCorrelationResult
{
    public string? Status { get; init; }

    public long? QuoteEntryRevisionId { get; init; }
}
    private static string GetExecutionCategory(string? execType)
    {
        return execType switch
        {
            "F" => "TRADE",
            "G" => "TRADE_CORRECT",
            "H" => "TRADE_CANCEL",
            "0" => "NEW",
            "4" => "CANCELLED",
            "5" => "REPLACED",
            "8" => "REJECTED",
            "9" => "SUSPENDED",
            "A" => "PENDING_NEW",
            "C" => "EXPIRED",
            "D" => "RESTATED",
            "E" => "PENDING_REPLACE",
            "L" => "TRIGGERED",
            _ => "OTHER"
        };
    }    

    private static bool IsDuplicateExecutionException(
    DbUpdateException exception)
{
    var innerException = exception.InnerException;

    while (innerException != null)
    {
        if (innerException is PostgresException postgresException)
        {
            return postgresException.SqlState == "23505"
                && postgresException.ConstraintName ==
                    "IX_executions_SessionId_ExecId";
        }

        innerException = innerException.InnerException;
    }

    return false;
}
}
