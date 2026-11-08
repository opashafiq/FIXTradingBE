using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.Entities;

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
            await ProcessMessageAsync(message, stoppingToken);
        }
    }

    private async Task ProcessMessageAsync(
        IncomingFixMessage message,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<TradingDbContext>();

        var log = new FixMessageLog
        {
            SessionId = message.SessionId,
            MsgType = message.MsgType,
            Direction = "IN",
            RawMessage = message.RawMessage,
            IsProcessed = false,
            ReceivedAtUtc = message.ReceivedAtUtc
        };

        dbContext.FixMessageLogs.Add(log);

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Incoming FIX message saved. MsgType: {MsgType}, LogId: {LogId}",
            message.MsgType,
            log.Id);
    }
}