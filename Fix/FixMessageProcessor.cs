using Microsoft.EntityFrameworkCore;
using QuickFix;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Orders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Fix;

public class FixMessageProcessor
{
    private readonly TradingDbContext _dbContext;
    private readonly FixMessageDispatcher _dispatcher;
    private readonly ExecutionReportPersistenceService _persistenceService;
    private readonly ILogger<FixMessageProcessor> _logger;
    private readonly BusinessMessageRejectPersistenceService _businessMessageRejectPersistenceService;
    private readonly OrderCancelRejectPersistenceService _orderCancelRejectPersistenceService;
    private readonly SessionRejectPersistenceService _sessionRejectPersistenceService;

    public FixMessageProcessor(
        TradingDbContext dbContext,
        FixMessageDispatcher dispatcher,
        ExecutionReportPersistenceService persistenceService,
        ILogger<FixMessageProcessor> logger,
        BusinessMessageRejectPersistenceService businessMessageRejectPersistenceService,
        OrderCancelRejectPersistenceService orderCancelRejectPersistenceService,
        SessionRejectPersistenceService sessionRejectPersistenceService)
    {
        _dbContext = dbContext;
        _dispatcher = dispatcher;
        _persistenceService = persistenceService;
        _logger = logger;
        _businessMessageRejectPersistenceService = businessMessageRejectPersistenceService;
        _orderCancelRejectPersistenceService = orderCancelRejectPersistenceService;
        _sessionRejectPersistenceService = sessionRejectPersistenceService;
    }

    
    
public async Task ProcessAsync(
    long logId,
    Guid processingToken,
    CancellationToken cancellationToken)
    {
        var log = await _dbContext.FixMessageLogs
            .FirstOrDefaultAsync(
                x => x.Id == logId &&
                    x.ProcessingStatus == "PROCESSING" &&
                    x.ProcessingToken == processingToken,
                cancellationToken);

        if (log == null)
        {
            throw new InvalidOperationException(
                $"FIX message log {logId} was not found or processing ownership was lost.");
        }

        var fixMessage = new Message(log.RawMessage);

        var sessionId = new SessionID(
            log.BeginString,
            log.SenderCompID,
            log.TargetCompID,
            log.SessionQualifier);

        var report = _dispatcher.Dispatch(fixMessage, sessionId);

        

        if (report != null)
        {
            await _persistenceService.PersistAsync(
                report,
                logId,
                processingToken,
                cancellationToken);
        }
        else if (log.MsgType == "j")
        {
            var reject = _dispatcher.ParseBusinessMessageReject(
                fixMessage,
                sessionId);

            await _businessMessageRejectPersistenceService.PersistAsync(
                reject,
                logId,
                processingToken,
                cancellationToken);
        }
        else if (log.MsgType == "9")
        {
            var reject = _dispatcher.ParseOrderCancelReject(
                fixMessage,
                sessionId);

            await _orderCancelRejectPersistenceService.PersistAsync(
                reject,
                logId,
                processingToken,
                cancellationToken);
        }
        else if (log.MsgType == "3")
        {
            var reject = _dispatcher.ParseSessionReject(
                fixMessage,
                sessionId);

            await _sessionRejectPersistenceService.PersistAsync(
                reject,
                logId,
                processingToken,
                cancellationToken);
        }

        _logger.LogInformation(
            "FIX message dispatched successfully. LogId: {LogId}, MsgType: {MsgType}",
            log.Id,
            log.MsgType);
    }


    
    
public async Task UpdateMessageStatusAsync(
    long logId,
    string status,
    string? processingError,
    Guid processingToken,
    CancellationToken cancellationToken)
{
    const int maxAttempts = 5;

    if (status != "PROCESSED" && status != "FAILED")
    {
        throw new ArgumentException(
            "Only PROCESSED or FAILED status is supported.",
            nameof(status));
    }

    var log = await _dbContext.FixMessageLogs
        .AsNoTracking()
        .FirstOrDefaultAsync(
            x => x.Id == logId &&
                 x.ProcessingStatus == "PROCESSING" &&
                 x.ProcessingToken == processingToken,
            cancellationToken);

    if (log == null)
    {
        throw new InvalidOperationException(
            $"FIX message log {logId} is no longer owned by this processor.");
    }

    var now = DateTimeOffset.UtcNow;

    var newRetryCount = status == "FAILED"
        ? log.RetryCount + 1
        : log.RetryCount;

    DateTimeOffset? nextRetryAtUtc = null;

    if (status == "FAILED" && newRetryCount < maxAttempts)
    {
        nextRetryAtUtc = now.AddSeconds(
            Math.Pow(2, newRetryCount));
    }

    int affectedRows = await _dbContext.FixMessageLogs
        .Where(x =>
            x.Id == logId &&
            x.ProcessingStatus == "PROCESSING" &&
            x.ProcessingToken == processingToken &&
            x.RetryCount == log.RetryCount)
        .ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.ProcessingStatus, status)
            .SetProperty(x => x.IsProcessed, status == "PROCESSED")
            .SetProperty(x => x.ProcessingError, processingError)
            .SetProperty(x => x.RetryCount, newRetryCount)
            .SetProperty(x => x.ProcessingStartedAtUtc, (DateTimeOffset?)null)
            .SetProperty(x => x.ProcessingToken, (Guid?)null)
            .SetProperty(x => x.NextRetryAtUtc, nextRetryAtUtc),
            cancellationToken);

    if (affectedRows == 0)
    {
        throw new InvalidOperationException(
            $"FIX message log {logId} status was not updated because processing ownership changed.");
    }

    _logger.LogInformation(
        "FIX message status updated. LogId: {LogId}, Status: {Status}, RetryCount: {RetryCount}",
        logId,
        status,
        newRetryCount);
}



}