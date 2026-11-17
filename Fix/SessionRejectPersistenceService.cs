
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Orders;

namespace TradingBackend.Api.Fix;

public class SessionRejectPersistenceService
{
    private readonly TradingDbContext _dbContext;
    private readonly ILogger<SessionRejectPersistenceService> _logger;

    public SessionRejectPersistenceService(
        TradingDbContext dbContext,
        ILogger<SessionRejectPersistenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task PersistAsync(
        SessionRejectDto reject,
        long logId,
        Guid processingToken,
        CancellationToken cancellationToken = default)
    {
        if (!reject.RefSeqNum.HasValue)
        {
            throw new InvalidOperationException(
                "Session Reject does not contain RefSeqNum (45).");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        // Verify ownership of the incoming Session Reject log.
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

        // Find the rejected outgoing message.
        var outgoingLog = await _dbContext.FixMessageLogs
            .FirstOrDefaultAsync(x =>
                x.SessionId == reject.SessionId &&
                x.Direction == "OUT" &&
                x.MsgSeqNum == reject.RefSeqNum.Value,
                cancellationToken);

        if (outgoingLog == null)
        {
            throw new InvalidOperationException(
                "Could not find the rejected outgoing FIX message. " +
                $"Session: {reject.SessionId}, " +
                $"RefSeqNum: {reject.RefSeqNum}");
        }

        // Verify the rejected message type when CHSE supplies RefMsgType.
        if (!string.IsNullOrWhiteSpace(reject.RefMsgType) &&
            outgoingLog.MsgType != reject.RefMsgType)
        {
            throw new InvalidOperationException(
                "Session Reject RefMsgType does not match the outgoing message. " +
                $"Expected: {reject.RefMsgType}, " +
                $"Actual: {outgoingLog.MsgType}");
        }

        // This service updates cancellation requests only.
        if (outgoingLog.MsgType != "F")
        {
            _logger.LogWarning(
                "Session Reject refers to a non-cancellation message. " +
                "No cancellation request updated. " +
                "Session: {SessionId}, SeqNum: {SeqNum}, MsgType: {MsgType}",
                reject.SessionId,
                reject.RefSeqNum,
                outgoingLog.MsgType);

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(outgoingLog.ClOrdId))
        {
            throw new InvalidOperationException(
                "Rejected cancellation message has no ClOrdID.");
        }

        // Find the cancellation request using its unique session/ClOrdID.
        var cancellationRequest =
            await _dbContext.OrderCancellationRequests
                .FirstOrDefaultAsync(x =>
                    x.SessionId == outgoingLog.SessionId &&
                    x.ClOrdId == outgoingLog.ClOrdId,
                    cancellationToken);

        if (cancellationRequest == null)
        {
            throw new InvalidOperationException(
                "No cancellation request matches the rejected outgoing message. " +
                $"ClOrdID: {outgoingLog.ClOrdId}");
        }

        // Do not overwrite an already terminal cancellation status.
        if (cancellationRequest.Status != "PendingSubmission" &&
            cancellationRequest.Status != "SubmittedToFix" &&
            cancellationRequest.Status != "SubmissionUnknown")
        {
            _logger.LogWarning(
                "Session Reject received for a cancellation request " +
                "that is no longer active. CancellationRequestId: {Id}, " +
                "Status: {Status}",
                cancellationRequest.Id,
                cancellationRequest.Status);

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var rejectionDetails =
            $"FIX Session Reject. " +
            $"RefSeqNum(45): {reject.RefSeqNum}; " +
            $"RefTagID(371): {reject.RefTagID ?? "Unknown"}; " +
            $"RefMsgType(372): {reject.RefMsgType ?? "Unknown"}; " +
            $"SessionRejectReason(373): " +
            $"{reject.SessionRejectReason?.ToString() ?? "Unknown"}; " +
            $"Text(58): {reject.Text ?? "No text provided"}";

        cancellationRequest.Status = "Rejected";
        cancellationRequest.SubmissionError = rejectionDetails;
        cancellationRequest.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogWarning(
            "Cancellation request rejected by FIX session. " +
            "TradingOrderId: {TradingOrderId}, " +
            "CancellationRequestId: {CancellationRequestId}, " +
            "ClOrdID: {ClOrdId}, RefSeqNum: {RefSeqNum}, " +
            "RefTagID: {RefTagID}, Reason: {Reason}, Text: {Text}",
            cancellationRequest.TradingOrderId,
            cancellationRequest.Id,
            cancellationRequest.ClOrdId,
            reject.RefSeqNum,
            reject.RefTagID,
            reject.SessionRejectReason,
            reject.Text);
    }
}
