
using TradingBackend.Api.Data;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Fix;

public class OutgoingFixMessageWorker : BackgroundService
{
    private readonly IOutgoingFixMessageQueue _messageQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutgoingFixMessageWorker> _logger;

    public OutgoingFixMessageWorker(
        IOutgoingFixMessageQueue messageQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<OutgoingFixMessageWorker> logger)
    {
        _messageQueue = messageQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Outgoing FIX message worker started.");

        await foreach (var message in
            _messageQueue.ReadAllAsync(stoppingToken))
        {
            await PersistMessageWithRetryAsync(
                message,
                stoppingToken);
        }
    }

    private async Task PersistMessageWithRetryAsync(
        IncomingFixMessage message,
        CancellationToken cancellationToken)
    {
        int attempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var dbContext = scope.ServiceProvider
                    .GetRequiredService<TradingDbContext>();

                string? clOrdId = message.ClOrdId;

                var log = new FixMessageLog
                {
                    SessionId = message.SessionId,
                    BeginString = message.BeginString,
                    SenderCompID = message.SenderCompID,
                    TargetCompID = message.TargetCompID,
                    SessionQualifier = message.SessionQualifier,

                    MsgType = message.MsgType,
                    Direction = "OUT",
                    ClOrdId = clOrdId,

                    RawMessage = message.RawMessage,

                    MsgSeqNum = message.MsgSeqNum,
                    PossDupFlag = message.PossDupFlag,
                    PossResend = message.PossResend,
                    SendingTime = message.SendingTime,
                    OrigSendingTime = message.OrigSendingTime,

                    IsProcessed = true,
                    ProcessingStatus = "PROCESSED",
                    ReceivedAtUtc = message.ReceivedAtUtc
                };

                dbContext.FixMessageLogs.Add(log);

                await dbContext.SaveChangesAsync(
                    cancellationToken);

                _logger.LogInformation(
                    "Outgoing FIX message persisted. " +
                    "MsgType={MsgType}, SeqNum={SeqNum}, " +
                    "ClOrdID={ClOrdId}, Session={SessionId}",
                    message.MsgType,
                    message.MsgSeqNum,
                    clOrdId,
                    message.SessionId);

                return;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                attempt++;

                int delaySeconds = Math.Min(
                    60,
                    (int)Math.Pow(2, Math.Min(attempt, 6)));

                _logger.LogError(
                    ex,
                    "Failed to persist outgoing FIX message. " +
                    "MsgType={MsgType}, SeqNum={SeqNum}, Attempt={Attempt}. " +
                    "Retrying in {DelaySeconds} seconds.",
                    message.MsgType,
                    message.MsgSeqNum,
                    attempt,
                    delaySeconds);

                await Task.Delay(
                    TimeSpan.FromSeconds(delaySeconds),
                    cancellationToken);
            }
        }
    }
}
