
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Orders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Fix;

public class OrderCancelRejectPersistenceService
{
    private readonly TradingDbContext _dbContext;
    private readonly ILogger<OrderCancelRejectPersistenceService> _logger;

    public OrderCancelRejectPersistenceService(
        TradingDbContext dbContext,
        ILogger<OrderCancelRejectPersistenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task PersistAsync(
        OrderCancelRejectDto reject,
        long logId,
        Guid processingToken,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        // Verify processing ownership and lock the incoming log row.
        int ownershipConfirmed = await _dbContext.FixMessageLogs
            .Where(x =>
                x.Id == logId &&
                x.ProcessingStatus == "PROCESSING" &&
                x.ProcessingToken == processingToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    x => x.ProcessingStartedAtUtc,
                    DateTimeOffset.UtcNow),
                cancellationToken);

        if (ownershipConfirmed == 0)
        {
            throw new InvalidOperationException(
                $"FIX message processing ownership lost. LogId: {logId}");
        }

 

        long? tradingOrderId = null;

        // First attempt: correlate using the cancellation request ClOrdID.
        if (!string.IsNullOrWhiteSpace(reject.ClOrdId))
        {
            tradingOrderId = await _dbContext.OrderIdentifiers
                .Where(x =>
                    x.SessionId == reject.SessionId &&
                    x.ClOrdId == reject.ClOrdId)
                .Select(x => (long?)x.TradingOrderId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Second attempt: correlate using the original order ClOrdID.
        if (tradingOrderId == null &&
            !string.IsNullOrWhiteSpace(reject.OrigClOrdId))
        {
            tradingOrderId = await _dbContext.OrderIdentifiers
                .Where(x =>
                    x.SessionId == reject.SessionId &&
                    x.ClOrdId == reject.OrigClOrdId)
                .Select(x => (long?)x.TradingOrderId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Final attempt: correlate using the exchange OrderID.
        if (tradingOrderId == null &&
            !string.IsNullOrWhiteSpace(reject.OrderId))
        {
            tradingOrderId = await _dbContext.OrderIdentifiers
                .Where(x =>
                    x.SessionId == reject.SessionId &&
                    x.TradingOrder.ExchangeOrderId == reject.OrderId)
                .Select(x => (long?)x.TradingOrderId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var record = new OrderCancelReject
        {
            FixMessageLogId = logId,
            SessionId = reject.SessionId,
            OrderId = reject.OrderId,
            ClOrdId = reject.ClOrdId,
            OrigClOrdId = reject.OrigClOrdId,
            OrdStatus = reject.OrdStatus,
            CxlRejResponseTo = reject.CxlRejResponseTo,
            CxlRejReason = reject.CxlRejReason,
            Text = reject.Text,
            TradingOrderId = tradingOrderId,
            ReceivedAtUtc = DateTimeOffset.UtcNow
        };


        // Update the matching cancellation request when this is a
        // rejection of an Order Cancel Request (35=F).
        if (reject.CxlRejResponseTo == 1 &&
            !string.IsNullOrWhiteSpace(reject.ClOrdId))
        {
            var cancellationRequest =
                await _dbContext.OrderCancellationRequests
                    .FirstOrDefaultAsync(x =>
                        x.SessionId == reject.SessionId &&
                        x.ClOrdId == reject.ClOrdId,
                        cancellationToken);

            if (cancellationRequest != null &&
                (cancellationRequest.Status == "PendingSubmission" ||
                cancellationRequest.Status == "SubmittedToFix" ||
                cancellationRequest.Status == "SubmissionUnknown"))
            {
                cancellationRequest.Status = "Rejected";

                cancellationRequest.SubmissionError =
                    $"OrderCancelReject: Reason={reject.CxlRejReason}; " +
                    $"Text={reject.Text}";

                cancellationRequest.UpdatedAtUtc = DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "Cancellation request rejected by exchange. " +
                    "TradingOrderId={TradingOrderId}, " +
                    "CancellationRequestId={CancellationRequestId}, " +
                    "ClOrdID={ClOrdId}, Reason={Reason}",
                    cancellationRequest.TradingOrderId,
                    cancellationRequest.Id,
                    cancellationRequest.ClOrdId,
                    reject.CxlRejReason);
            }
        }

// Update the matching replacement request when this is a
// rejection of an Order Cancel/Replace Request (35=G).
if (reject.CxlRejResponseTo == 2 &&
    !string.IsNullOrWhiteSpace(reject.ClOrdId))
{
    var replacementRequest =
        await _dbContext.OrderReplacementRequests
            .FirstOrDefaultAsync(x =>
                x.SessionId == reject.SessionId &&
                x.ClOrdId == reject.ClOrdId,
                cancellationToken);

    if (replacementRequest != null &&
        (replacementRequest.Status == "PendingSubmission" ||
         replacementRequest.Status == "SubmittedToFix" ||
         replacementRequest.Status == "SubmissionUnknown" ||
         replacementRequest.Status == "PendingReplace"))
    {
        replacementRequest.Status = "Rejected";

        replacementRequest.SubmissionError =
            $"OrderCancelReject: Reason={reject.CxlRejReason}; " +
            $"Text={reject.Text}";

        replacementRequest.UpdatedAtUtc = DateTimeOffset.UtcNow;

        // The exchange-reported OrdStatus represents the status
        // of the original order after the replacement was rejected.
        var replacementOrder =
            await _dbContext.Orders
                .FirstOrDefaultAsync(
                    x => x.Id == replacementRequest.TradingOrderId,
                    cancellationToken);

        if (replacementOrder != null &&
            !string.IsNullOrWhiteSpace(reject.OrdStatus))
        {
            replacementOrder.OrdStatus = reject.OrdStatus;
            replacementOrder.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        _logger.LogInformation(
            "Replacement request rejected by exchange. " +
            "TradingOrderId={TradingOrderId}, " +
            "ReplacementRequestId={ReplacementRequestId}, " +
            "ClOrdID={ClOrdId}, Reason={Reason}, OrdStatus={OrdStatus}",
            replacementRequest.TradingOrderId,
            replacementRequest.Id,
            replacementRequest.ClOrdId,
            reject.CxlRejReason,
            reject.OrdStatus);
    }
}

        // Prevent duplicate persistence during message recovery.
        // Reconciliation above must still run for an existing record.
        bool alreadyExists = await _dbContext.OrderCancelRejects
            .AnyAsync(
                x => x.FixMessageLogId == logId,
                cancellationToken);

        if (alreadyExists)
        {
            _logger.LogInformation(
                "Duplicate OrderCancelReject ignored after reconciliation. " +
                "LogId: {LogId}",
                logId);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        _dbContext.OrderCancelRejects.Add(record);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (tradingOrderId.HasValue)
        {
            _logger.LogWarning(
                "OrderCancelReject persisted and correlated. " +
                "LogId: {LogId}, TradingOrderId: {TradingOrderId}, " +
                "ClOrdId: {ClOrdId}, OrigClOrdId: {OrigClOrdId}, " +
                "Reason: {Reason}",
                logId,
                tradingOrderId,
                reject.ClOrdId,
                reject.OrigClOrdId,
                reject.CxlRejReason);
        }
        else
        {
            _logger.LogWarning(
                "OrderCancelReject persisted without matching order. " +
                "LogId: {LogId}, Session: {SessionId}, " +
                "ClOrdId: {ClOrdId}, OrigClOrdId: {OrigClOrdId}, " +
                "OrderId: {OrderId}",
                logId,
                reject.SessionId,
                reject.ClOrdId,
                reject.OrigClOrdId,
                reject.OrderId);
        }
    }
}
