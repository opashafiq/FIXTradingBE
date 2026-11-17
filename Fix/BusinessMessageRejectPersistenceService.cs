
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Orders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Fix;

public class BusinessMessageRejectPersistenceService
{
    private readonly TradingDbContext _dbContext;
    private readonly ILogger<BusinessMessageRejectPersistenceService> _logger;

    public BusinessMessageRejectPersistenceService(
        TradingDbContext dbContext,
        ILogger<BusinessMessageRejectPersistenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task PersistAsync(
        BusinessMessageRejectDto reject,
        long logId,
        Guid processingToken,
        CancellationToken cancellationToken = default)
    {
        if (!reject.BusinessRejectReason.HasValue)
        {
            throw new InvalidOperationException(
                "BusinessMessageReject is missing BusinessRejectReason (tag 380).");
        }

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

        // Prevent duplicate persistence during message recovery.
        bool alreadyExists = await _dbContext.BusinessMessageRejects
            .AnyAsync(
                x => x.FixMessageLogId == logId,
                cancellationToken);

        if (alreadyExists)
        {
            _logger.LogInformation(
                "Duplicate BusinessMessageReject ignored. LogId: {LogId}",
                logId);

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        long? tradingOrderId = null;

        // First attempt: correlate using tag 379.
        if (!string.IsNullOrWhiteSpace(reject.BusinessRejectRefID))
        {
            tradingOrderId = await _dbContext.OrderIdentifiers
                .Where(x =>
                    x.SessionId == reject.SessionId &&
                    x.ClOrdId == reject.BusinessRejectRefID)
                .Select(x => (long?)x.TradingOrderId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Fallback: correlate using tags 45 and 372.
        if (tradingOrderId == null &&
            reject.RefSeqNum.HasValue &&
            !string.IsNullOrWhiteSpace(reject.RefMsgType))
        {
            var outgoingClOrdId = await _dbContext.FixMessageLogs
                .Where(x =>
                    x.SessionId == reject.SessionId &&
                    x.Direction == "OUT" &&
                    x.MsgSeqNum == reject.RefSeqNum.Value &&
                    x.MsgType == reject.RefMsgType)
                .OrderByDescending(x => x.Id)
                .Select(x => x.ClOrdId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(outgoingClOrdId))
            {
                tradingOrderId = await _dbContext.OrderIdentifiers
                    .Where(x =>
                        x.SessionId == reject.SessionId &&
                        x.ClOrdId == outgoingClOrdId)
                    .Select(x => (long?)x.TradingOrderId)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        var record = new BusinessMessageRejectRecord
        {
            FixMessageLogId = logId,
            SessionId = reject.SessionId,
            BusinessRejectRefID = reject.BusinessRejectRefID,
            RefSeqNum = reject.RefSeqNum,
            RefMsgType = reject.RefMsgType,
            RefTagID = reject.RefTagID,
            BusinessRejectReason = reject.BusinessRejectReason.Value,
            Text = reject.Text,
            TradingOrderId = tradingOrderId,
            ReceivedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.BusinessMessageRejects.Add(record);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (tradingOrderId.HasValue)
        {
            _logger.LogWarning(
                "BusinessMessageReject persisted and correlated. " +
                "LogId: {LogId}, TradingOrderId: {TradingOrderId}, " +
                "RefMsgType: {RefMsgType}, Reason: {Reason}",
                logId,
                tradingOrderId,
                reject.RefMsgType,
                reject.BusinessRejectReason);
        }
        else
        {
            _logger.LogWarning(
                "BusinessMessageReject persisted without matching order. " +
                "LogId: {LogId}, Session: {SessionId}, " +
                "BusinessRejectRefID: {BusinessRejectRefID}, " +
                "RefSeqNum: {RefSeqNum}, RefMsgType: {RefMsgType}",
                logId,
                reject.SessionId,
                reject.BusinessRejectRefID,
                reject.RefSeqNum,
                reject.RefMsgType);
        }
    }
}
