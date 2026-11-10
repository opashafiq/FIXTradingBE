
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;


namespace TradingBackend.Api.Fix;

public class FixMessageRecoveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FixMessageRecoveryWorker> _logger;

    public FixMessageRecoveryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<FixMessageRecoveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "FIX message recovery worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverStaleMessagesAsync(stoppingToken);

                await RecoverMessagesAsync(stoppingToken);
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
                    "Error in FIX message recovery worker.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(5),
                stoppingToken);
        }
    }

    private async Task RecoverMessagesAsync(
    CancellationToken cancellationToken)
{
    const int batchSize = 20;
    const int maxAttempts = 5;

    // First, find messages that are eligible for retry.
    List<long> candidateIds;

    await using (var scope = _scopeFactory.CreateAsyncScope())
    {
        var dbContext = scope.ServiceProvider
            .GetRequiredService<TradingDbContext>();

        var now = DateTimeOffset.UtcNow;

        candidateIds = await dbContext.FixMessageLogs
            .AsNoTracking()
            .Where(x =>
                x.Direction == "IN" &&
                x.ProcessingStatus == "FAILED" &&
                x.RetryCount < maxAttempts &&
                x.NextRetryAtUtc != null &&
                x.NextRetryAtUtc <= now &&
                x.BeginString != "" &&
                x.SenderCompID != "" &&
                x.TargetCompID != "")
            .OrderBy(x => x.NextRetryAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    foreach (long logId in candidateIds)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = _scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<TradingDbContext>();

        // Atomically claim the message.
        // Another worker cannot claim it after its status changes.
        int claimed = await dbContext.FixMessageLogs
            .Where(x =>
                x.Id == logId &&
                x.Direction == "IN" &&
                x.ProcessingStatus == "FAILED" &&
                x.RetryCount < maxAttempts &&
                x.NextRetryAtUtc != null &&
                x.NextRetryAtUtc <= DateTimeOffset.UtcNow)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ProcessingStatus, "PROCESSING")
                .SetProperty(x => x.ProcessingError, (string?)null)
                .SetProperty(x => x.NextRetryAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.ProcessingStartedAtUtc,DateTimeOffset.UtcNow),
                cancellationToken);

        // Another worker may already have claimed this message.
        if (claimed == 0)
            continue;

        _logger.LogInformation(
            "Recovering FIX message. LogId: {LogId}",
            logId);

        var processor = scope.ServiceProvider
            .GetRequiredService<FixMessageProcessor>();

        try
        {
            await processor.ProcessAsync(logId, cancellationToken);

            await processor.UpdateMessageStatusAsync(
                logId,
                "PROCESSED",
                null,
                cancellationToken);

            _logger.LogInformation(
                "FIX message recovery successful. LogId: {LogId}",
                logId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "FIX message recovery failed. LogId: {LogId}",
                logId);

            try
            {
                await processor.UpdateMessageStatusAsync(
                    logId,
                    "FAILED",
                    ex.Message,
                    cancellationToken);
            }
            catch (Exception statusException)
            {
                _logger.LogError(
                    statusException,
                    "Could not update recovery status. LogId: {LogId}",
                    logId);
            }
        }
    }
}

private async Task ProcessClaimedMessageAsync(
    long logId,
    CancellationToken cancellationToken)
{
    string? processingError = null;

    // Process the message in its own dependency-injection scope.
    try
    {
        await using var processingScope =
            _scopeFactory.CreateAsyncScope();

        var processor = processingScope.ServiceProvider
            .GetRequiredService<FixMessageProcessor>();

        await processor.ProcessAsync(logId, cancellationToken);
    }
    catch (Exception ex)
    {
        processingError = ex.Message;

        _logger.LogError(
            ex,
            "Failed to recover FIX message. LogId: {LogId}",
            logId);
    }

    // Update the status using a fresh scope.
    try
    {
        await using var statusScope =
            _scopeFactory.CreateAsyncScope();

        var processor = statusScope.ServiceProvider
            .GetRequiredService<FixMessageProcessor>();

        await processor.UpdateMessageStatusAsync(
            logId,
            processingError == null ? "PROCESSED" : "FAILED",
            processingError,
            cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Could not update recovered FIX message status. LogId: {LogId}",
            logId);
    }
}

private async Task RecoverStaleMessagesAsync(
    CancellationToken cancellationToken)
{
    const int maxAttempts = 5;

    var now = DateTimeOffset.UtcNow;
    var staleThreshold = now.AddMinutes(-10);

    await using var scope = _scopeFactory.CreateAsyncScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<TradingDbContext>();

    int affectedRows = await dbContext.FixMessageLogs
        .Where(x =>
            x.Direction == "IN" &&
            x.ProcessingStatus == "PROCESSING" &&
            x.ProcessingStartedAtUtc != null &&
            x.ProcessingStartedAtUtc < staleThreshold)
        .ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.ProcessingStatus, "FAILED")
            .SetProperty(
                x => x.RetryCount,
                x => x.RetryCount + 1)
            .SetProperty(
                x => x.ProcessingError,
                "Processing timeout. Message marked for recovery.")
            .SetProperty(
                x => x.ProcessingStartedAtUtc,
                (DateTimeOffset?)null)
            .SetProperty(
                x => x.NextRetryAtUtc,
                x => x.RetryCount + 1 < maxAttempts
                    ? now
                    : (DateTimeOffset?)null),
            cancellationToken);

    if (affectedRows > 0)
    {
        _logger.LogWarning(
            "{Count} stale FIX messages marked as FAILED.",
            affectedRows);
    }
}

}
