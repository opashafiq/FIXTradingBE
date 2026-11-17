
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

    // Step 1: Find messages eligible for retry.
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

    // Step 2: Process each eligible message.
    foreach (long logId in candidateIds)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = _scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<TradingDbContext>();

        var processingToken = Guid.NewGuid();

        // Step 3: Atomically claim the message.
        int claimed = await dbContext.FixMessageLogs
            .Where(x =>
                x.Id == logId &&
                x.Direction == "IN" &&
                x.ProcessingStatus == "FAILED" &&
                x.RetryCount < maxAttempts &&
                x.NextRetryAtUtc != null &&
                x.NextRetryAtUtc <= DateTimeOffset.UtcNow)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    x => x.ProcessingStatus,
                    "PROCESSING")
                .SetProperty(
                    x => x.ProcessingError,
                    (string?)null)
                .SetProperty(
                    x => x.NextRetryAtUtc,
                    (DateTimeOffset?)null)
                .SetProperty(
                    x => x.ProcessingStartedAtUtc,
                    DateTimeOffset.UtcNow)
                .SetProperty(
                    x => x.ProcessingToken,
                    (Guid?)processingToken),
                cancellationToken);

        // Another worker may already have claimed this message.
        if (claimed == 0)
            continue;

        _logger.LogInformation(
            "Recovering FIX message. LogId: {LogId}",
            logId);

        // Step 4: Process the claimed message.
        await ProcessClaimedMessageAsync(
            logId,
            processingToken,
            cancellationToken);
    }
}






private async Task ProcessClaimedMessageAsync(
    long logId,
    Guid processingToken,
    CancellationToken cancellationToken)
{
    string? processingError = null;
    bool leaseLost = false;

    using var processingCts =
        CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

    // Start the lease heartbeat.
    Task<bool> heartbeatTask = RenewProcessingLeaseAsync(
        logId,
        processingToken,
        processingCts);

    try
    {
        await using var processingScope =
            _scopeFactory.CreateAsyncScope();

        var processor = processingScope.ServiceProvider
            .GetRequiredService<FixMessageProcessor>();

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
            "FIX message recovery cancelled. LogId: {LogId}",
            logId);
    }
    catch (OperationCanceledException)
        when (processingCts.IsCancellationRequested)
    {
        leaseLost = true;

        _logger.LogWarning(
            "FIX message recovery stopped because its processing lease was lost. LogId: {LogId}",
            logId);
    }
    catch (Exception ex)
    {
        processingError = ex.Message;

        _logger.LogError(
            ex,
            "FIX message recovery failed. LogId: {LogId}",
            logId);
    }
    finally
    {
        // Stop the heartbeat and wait for it to finish.
        processingCts.Cancel();

        try
        {
            leaseLost |= await heartbeatTask;
        }
        catch (Exception ex)
        {
            leaseLost = true;

            _logger.LogError(
                ex,
                "Error stopping FIX message heartbeat. LogId: {LogId}",
                logId);
        }
    }

    // Do not update status if this worker no longer owns the message.
    if (leaseLost)
    {
        _logger.LogWarning(
            "Skipping recovery status update because the processing lease was lost. LogId: {LogId}",
            logId);

        cancellationToken.ThrowIfCancellationRequested();
        return;
    }

    // During application shutdown, make a best effort to persist status.
    var statusCancellationToken =
        cancellationToken.IsCancellationRequested
            ? CancellationToken.None
            : cancellationToken;

    try
    {
        await UpdateRecoveryStatusAsync(
            logId,
            processingToken,
            processingError == null ? "PROCESSED" : "FAILED",
            processingError,
            statusCancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Could not update recovery status. LogId: {LogId}",
            logId);
    }

    cancellationToken.ThrowIfCancellationRequested();
}



private async Task UpdateRecoveryStatusAsync(
    long logId,
    Guid processingToken,
    string status,
    string? processingError,
    CancellationToken cancellationToken)
{
    await using var statusScope =
        _scopeFactory.CreateAsyncScope();

    var processor = statusScope.ServiceProvider
        .GetRequiredService<FixMessageProcessor>();

    await processor.UpdateMessageStatusAsync(
        logId,
        status,
        processingError,
        processingToken,
        cancellationToken);
}



private async Task RecoverStaleMessagesAsync(
    CancellationToken cancellationToken)
{
    const int maxAttempts = 5;

    var now = DateTimeOffset.UtcNow;

    // Allow sufficient time for normal message processing.
    var staleThreshold = now.AddMinutes(-30);

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
            .SetProperty(
                x => x.ProcessingStatus,
                "FAILED")
            .SetProperty(
                x => x.RetryCount,
                x => x.RetryCount < maxAttempts
                    ? x.RetryCount + 1
                    : maxAttempts)
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
                    : (DateTimeOffset?)null)
            .SetProperty(
                x => x.ProcessingToken,
                (Guid?)null),
            cancellationToken);

    if (affectedRows > 0)
    {
        _logger.LogWarning(
            "{Count} stale FIX messages marked as FAILED.",
            affectedRows);
    }
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

        return false;
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

        // Fail closed if lease renewal cannot be confirmed.
        processingCts.Cancel();
        return true;
    }
}




}
