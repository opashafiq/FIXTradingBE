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

    public FixMessageProcessor(
        TradingDbContext dbContext,
        FixMessageDispatcher dispatcher,
        ExecutionReportPersistenceService persistenceService,
        ILogger<FixMessageProcessor> logger)
    {
        _dbContext = dbContext;
        _dispatcher = dispatcher;
        _persistenceService = persistenceService;
        _logger = logger;
    }

    
    
    public async Task ProcessAsync(
        long logId,
        CancellationToken cancellationToken)
    {
        var log = await _dbContext.FixMessageLogs
            .FirstOrDefaultAsync(
                x => x.Id == logId,
                cancellationToken);

        if (log == null)
        {
            throw new InvalidOperationException(
                $"FIX message log {logId} was not found.");
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
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;

        var log = await _dbContext.FixMessageLogs
            .FirstOrDefaultAsync(
                x => x.Id == logId,
                cancellationToken);

        if (log == null)
        {
            throw new InvalidOperationException(
                $"FIX message log {logId} was not found.");
        }

        log.ProcessingStatus = status;
        log.IsProcessed = status == "PROCESSED";
        log.ProcessingError = processingError;

        if (status == "PROCESSING")
        {
            log.ProcessingStartedAtUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            log.ProcessingStartedAtUtc = null;
        }

        if (status == "FAILED")
        {
            log.RetryCount++;

            if (log.RetryCount < maxAttempts)
            {
                int delaySeconds =
                    (int)Math.Pow(2, log.RetryCount);

                log.NextRetryAtUtc = DateTimeOffset.UtcNow
                    .AddSeconds(delaySeconds);
            }
            else
            {
                log.NextRetryAtUtc = null;
            }
        }
        else if (status == "PROCESSED")
        {
            log.NextRetryAtUtc = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }


}