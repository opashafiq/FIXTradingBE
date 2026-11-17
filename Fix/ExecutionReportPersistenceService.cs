
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

    public ExecutionReportPersistenceService(
        TradingDbContext dbContext,
        ILogger<ExecutionReportPersistenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
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
        MdEntryId = report.MdEntryId,
        OrderCapacity = report.OrderCapacity,
        TrdMatchId = report.TrdMatchId,
        SecondaryTradeId = report.SecondaryTradeId,
        ExecRestatementReason = report.ExecRestatementReason,
        OrdRejReason = report.OrdRejReason
    };

    _dbContext.Executions.Add(execution);

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
    else
    {
        _logger.LogWarning(
            "No matching order found for ExecutionReport. SessionId={SessionId}, ClOrdID={ClOrdId}",
            report.SessionId,
            report.ClOrdId);
    }

    try
    {
        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
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
