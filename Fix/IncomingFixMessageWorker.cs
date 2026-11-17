
using Microsoft.EntityFrameworkCore;
using QuickFix;
using TradingBackend.Api.Data;
using TradingBackend.Api.Entities;
using Npgsql;

namespace TradingBackend.Api.Fix;

public class IncomingFixMessageWorker : BackgroundService
{
    private readonly IIncomingFixMessageQueue _messageQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IncomingFixMessageWorker> _logger;

    public IncomingFixMessageWorker(
        IIncomingFixMessageQueue messageQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<IncomingFixMessageWorker> logger)
    {
        _messageQueue = messageQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Incoming FIX message worker started.");

        await foreach (
            var message in _messageQueue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessMessageAsync(message, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process incoming FIX message. MsgType: {MsgType}",
                    message.MsgType);
            }
        }
    }


    private async Task SaveIncomingMessageWithRetryAsync(
        TradingDbContext dbContext,
        FixMessageLog log,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                dbContext.FixMessageLogs.Add(log);

                await dbContext.SaveChangesAsync(cancellationToken);

                return;
            }
            catch (Exception ex)
                when (attempt < maxAttempts
                    && IsTransientDatabaseException(ex))
            {
                // Remove the failed entity from the change tracker.
                dbContext.Entry(log).State = EntityState.Detached;

                int delaySeconds = (int)Math.Pow(2, attempt - 1);

                _logger.LogWarning(
                    ex,
                    "Database save failed. Retrying in {DelaySeconds}s. Attempt {Attempt}/{MaxAttempts}",
                    delaySeconds,
                    attempt,
                    maxAttempts);

                await Task.Delay(
                    TimeSpan.FromSeconds(delaySeconds),
                    cancellationToken);
            }
        }
    }

    
    private static bool IsTransientDatabaseException(Exception ex)
    {
        while (ex != null)
        {
            if (ex is NpgsqlException npgsqlException
                && npgsqlException.IsTransient)
            {
                return true;
            }

            ex = ex.InnerException!;
        }

        return false;
    }

    

private async Task ProcessMessageAsync(
    IncomingFixMessage message,
    CancellationToken cancellationToken)
{
    long logId;

    // Step 1: Save the original FIX message.
    await using (var logScope = _scopeFactory.CreateAsyncScope())
    {
        var logDb = logScope.ServiceProvider
            .GetRequiredService<TradingDbContext>();

        var log = new FixMessageLog
        {
            SessionId = message.SessionId,
            MsgType = message.MsgType,
            Direction = "IN",
            RawMessage = message.RawMessage,
            IsProcessed = false,
            ProcessingStatus = "PENDING",
            ReceivedAtUtc = message.ReceivedAtUtc,
            MsgSeqNum = message.MsgSeqNum,
            PossDupFlag = message.PossDupFlag,
            PossResend = message.PossResend,
            SendingTime = message.SendingTime,
            OrigSendingTime = message.OrigSendingTime,
            BeginString = message.BeginString,
            SenderCompID = message.SenderCompID,
            TargetCompID = message.TargetCompID,
            SessionQualifier = message.SessionQualifier
        };

        await SaveIncomingMessageWithRetryAsync(
            logDb,
            log,
            cancellationToken);

        logId = log.Id;
    }

    // Step 2: Generate a unique ownership token.
    var processingToken = Guid.NewGuid();

    // Step 3: Atomically claim the PENDING message.
    await using (var claimScope = _scopeFactory.CreateAsyncScope())
    {
        var claimDb = claimScope.ServiceProvider
            .GetRequiredService<TradingDbContext>();

        int claimed = await claimDb.FixMessageLogs
            .Where(x =>
                x.Id == logId &&
                x.Direction == "IN" &&
                x.ProcessingStatus == "PENDING")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    x => x.ProcessingStatus,
                    "PROCESSING")
                .SetProperty(
                    x => x.ProcessingError,
                    (string?)null)
                .SetProperty(
                    x => x.ProcessingStartedAtUtc,
                    DateTimeOffset.UtcNow)
                .SetProperty(
                    x => x.ProcessingToken,
                    (Guid?)processingToken),
                cancellationToken);

        if (claimed == 0)
        {
            throw new InvalidOperationException(
                $"Could not claim incoming FIX message. LogId: {logId}");
        }
    }

    _logger.LogInformation(
        "Incoming FIX message claimed for processing. LogId: {LogId}",
        logId);

    // Step 4: Process the message with heartbeat protection.
    await using var processingScope =
        _scopeFactory.CreateAsyncScope();

    var processor = processingScope.ServiceProvider
        .GetRequiredService<FixMessageProcessor>();

    using var processingCts =
        CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

    string? processingError = null;
    bool leaseLost = false;

    // Start heartbeat.
    var heartbeatTask = RenewProcessingLeaseAsync(
        logId,
        processingToken,
        processingCts);

    try
    {
        await processor.ProcessAsync(
            logId,
            processingToken,
            processingCts.Token);
    }
    catch (OperationCanceledException)
        when (cancellationToken.IsCancellationRequested)
    {
        processingError =
            "Processing cancelled because the application is shutting down.";

        _logger.LogWarning(
            "Incoming FIX message processing cancelled. LogId: {LogId}",
            logId);
    }
    catch (OperationCanceledException)
        when (processingCts.IsCancellationRequested)
    {
        leaseLost = true;

        _logger.LogWarning(
            "Incoming FIX message processing stopped because its lease was lost. LogId: {LogId}",
            logId);
    }
    catch (Exception ex)
    {
        processingError = ex.ToString();

        _logger.LogError(
            ex,
            "Incoming FIX message processing failed. LogId: {LogId}",
            logId);
    }
    finally
    {
        // Stop heartbeat and wait for completion.
        processingCts.Cancel();

        try
        {
            // Detect heartbeat failure even if message processing
            // completed just before the heartbeat failed.
            leaseLost |= await heartbeatTask;
        }
        catch (Exception ex)
        {
            leaseLost = true;

            _logger.LogError(
                ex,
                "Error stopping incoming FIX message heartbeat. LogId: {LogId}",
                logId);
        }
    }

    // Step 5: Do not update status if ownership was lost.
    if (leaseLost)
    {
        _logger.LogWarning(
            "Skipping status update because processing ownership was lost. LogId: {LogId}",
            logId);

        return;
    }

    // Step 6: Update final processing status.
    try
    {
        await processor.UpdateMessageStatusAsync(
            logId,
            processingError == null ? "PROCESSED" : "FAILED",
            processingError,
            processingToken,
            cancellationToken.IsCancellationRequested
                ? CancellationToken.None
                : cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Could not update incoming FIX message status. LogId: {LogId}",
            logId);

        if (cancellationToken.IsCancellationRequested)
            throw;
    }

    // Stop the worker if application shutdown was requested.
    if (cancellationToken.IsCancellationRequested)
    {
        cancellationToken.ThrowIfCancellationRequested();
    }

    _logger.LogInformation(
        "Incoming FIX message processing completed. LogId: {LogId}, Status: {Status}",
        logId,
        processingError == null ? "PROCESSED" : "FAILED");
}



private async Task<bool> RenewProcessingLeaseAsync(
    long logId,
    Guid processingToken,
    CancellationTokenSource processingCts)
{
    using var timer = new PeriodicTimer(
        TimeSpan.FromMinutes(5));

    try
    {
        while (await timer.WaitForNextTickAsync(
            processingCts.Token))
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<TradingDbContext>();

            var now = DateTimeOffset.UtcNow;

            int affectedRows = await dbContext.FixMessageLogs
                .Where(x =>
                    x.Id == logId &&
                    x.ProcessingStatus == "PROCESSING" &&
                    x.ProcessingToken == processingToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        x => x.ProcessingStartedAtUtc,
                        now),
                    processingCts.Token);

            if (affectedRows == 0)
            {
                _logger.LogWarning(
                    "FIX message lease lost. LogId: {LogId}",
                    logId);

                processingCts.Cancel();
                return true;
            }

            _logger.LogDebug(
                "FIX message lease renewed. LogId: {LogId}",
                logId);
        }
    }
    catch (OperationCanceledException)
        when (processingCts.IsCancellationRequested)
    {
        // Normal heartbeat shutdown.
        return false;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "FIX message heartbeat failed. LogId: {LogId}",
            logId);

        // Fail closed: stop processing if lease renewal fails.
        processingCts.Cancel();
        return true;
    }
    return false;
}


}
