using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using Microsoft.AspNetCore.SignalR;
using TradingBackend.Api.Hubs;

namespace TradingBackend.Api.Fix;

public class FixApplication : IApplication
{
    private readonly ILogger<FixApplication> _logger;
    private readonly IHubContext<FixStatusHub> _hubContext;
    private readonly FixMessageDispatcher _dispatcher;
    private readonly IIncomingFixMessageQueue _messageQueue;

    private volatile bool _isLoggedOn;
    private string? _sessionId;

    public bool IsLoggedOn => _isLoggedOn;
    public string? SessionId => _sessionId;

public FixApplication(
    ILogger<FixApplication> logger,
    IHubContext<FixStatusHub> hubContext,
    FixMessageDispatcher dispatcher,
    IIncomingFixMessageQueue messageQueue)
{
    _logger = logger;
    _hubContext = hubContext;
    _dispatcher = dispatcher;
    _messageQueue = messageQueue;
}
    public void OnCreate(SessionID sessionId)
    {
        _logger.LogInformation(
            "FIX session created: {SessionId}", sessionId);
    }

    public void OnLogon(SessionID sessionId)
    {
        _sessionId = sessionId.ToString();
        _isLoggedOn = true;

        _logger.LogInformation(
            "Successfully logged on to FIX session: {SessionId}",
            sessionId);

        _ = BroadcastConnectionStatusAsync();
    }

    public void OnLogout(SessionID sessionId)
    {
        _isLoggedOn = false;
        _logger.LogWarning(
            "FIX session logged out: {SessionId}", sessionId);

        _ = BroadcastConnectionStatusAsync();
    }

    public void FromAdmin(Message message, SessionID sessionId)
    {
        // Incoming administrative messages:
        // Logon, Logout, Heartbeat, TestRequest, etc.
        // QuickFIX/n handles session-level processing.
    }

public void ToAdmin(Message message, SessionID sessionId)
{
    // FIX Logon message (MsgType = A)
    if (message.Header.GetString(Tags.MsgType) == MsgType.LOGON)
    {
        string? password =
            Environment.GetEnvironmentVariable("CHSE_FIX_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "CHSE_FIX_PASSWORD environment variable is not configured."
            );
        }

        // Tag 554: Password
        message.SetField(
            new QuickFix.Fields.Password(password)
        );

        _logger.LogInformation(
            "CHSE FIX Logon authentication field configured."
        );
    }
}

    public void FromApp(Message message, SessionID sessionId)
    {
        try
        {
            string msgType = message.Header.GetString(Tags.MsgType);

            var incomingMessage = new IncomingFixMessage
            {
                SessionId = sessionId.ToString(),
                MsgType = msgType,
                RawMessage = message.ToString(),
                ReceivedAtUtc = DateTimeOffset.UtcNow
            };

            // Queue the raw message for database persistence.
            _messageQueue
                .EnqueueAsync(incomingMessage)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            // Invoke the appropriate FIX message handler.
            _dispatcher.Dispatch(message, sessionId);

            _logger.LogInformation(
                "Incoming FIX message queued and dispatched. MsgType: {MsgType}, Session: {SessionId}",
                msgType,
                sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process incoming FIX message. Session: {SessionId}",
                sessionId);

            throw;
        }
    }

    public void ToApp(Message message, SessionID sessionId)
    {
        // Outgoing application messages:
        // NewOrderSingle, OrderCancelRequest, etc.
    }

    private async Task BroadcastConnectionStatusAsync()
    {
        await _hubContext.Clients.All.SendAsync(
            "FixConnectionStatusChanged",
            new
            {
                Connected = IsLoggedOn,
                SessionId = SessionId
            }
        );
    }
}