
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

        // Step 1: Save the original FIX message using its own DbContext.
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
                SessionQualifier = message.SessionQualifier,
            };

            await SaveIncomingMessageWithRetryAsync(
                logDb,
                log,
                cancellationToken);

            logId = log.Id;
        }

        
        // Step 2: Mark the message as PROCESSING.
        await UpdateProcessingStatusAsync(
            logId,
            "PROCESSING",
            null,
            cancellationToken);

        string? processingError = null;

        try
        {
            await using var processingScope =
                _scopeFactory.CreateAsyncScope();

            var processor = processingScope.ServiceProvider
                .GetRequiredService<FixMessageProcessor>();

            await processor.ProcessAsync(
                logId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            processingError = ex.ToString();

            _logger.LogError(
                ex,
                "Error processing FIX message. LogId: {LogId}, MsgType: {MsgType}",
                logId,
                message.MsgType);
        }

        // Step 3: Update final processing status.
        await UpdateProcessingStatusAsync(
            logId,
            processingError == null ? "PROCESSED" : "FAILED",
            processingError,
            cancellationToken);

    }


   private async Task UpdateProcessingStatusAsync(
    long logId,
    string status,
    string? processingError,
    CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var processor = scope.ServiceProvider
            .GetRequiredService<FixMessageProcessor>();

        await processor.UpdateMessageStatusAsync(
            logId,
            status,
            processingError,
            cancellationToken);
    }

}
