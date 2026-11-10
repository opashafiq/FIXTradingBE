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
    private readonly IIncomingFixMessageQueue _messageQueue;

    private volatile bool _isLoggedOn;
    private string? _sessionId;
    public bool IsLoggedOn => _isLoggedOn;
    public string? SessionId => _sessionId;

    public SessionID? ActiveSession { get; private set; }

    

public FixApplication(
    ILogger<FixApplication> logger,
    IHubContext<FixStatusHub> hubContext,
    IIncomingFixMessageQueue messageQueue)
{
    _logger = logger;
    _hubContext = hubContext;
    _messageQueue = messageQueue;
}
    public void OnCreate(SessionID sessionId)
    {
        _logger.LogInformation(
            "FIX session created: {SessionId}", sessionId);
    }

public void OnLogon(SessionID sessionId)
{
    ActiveSession = sessionId;
    _isLoggedOn = true;
    _sessionId = sessionId.ToString();

    _logger.LogInformation(
        "Successfully logged on to FIX session: {SessionId}",
        _sessionId);

    _ = BroadcastConnectionStatusAsync();
}

    public void OnLogout(SessionID sessionId)
{
    ActiveSession = null;
    _isLoggedOn = false;
    _sessionId = null;

    _logger.LogWarning(
        "FIX session logged out: {SessionId}",
        sessionId);

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

            BeginString = sessionId.BeginString,
            SenderCompID = sessionId.SenderCompID,
            TargetCompID = sessionId.TargetCompID,
            SessionQualifier = sessionId.SessionQualifier ?? string.Empty,

            MsgType = msgType,
            RawMessage = message.ToString(),
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            MsgSeqNum = message.Header.GetInt(Tags.MsgSeqNum),

            PossDupFlag = message.Header.IsSetField(Tags.PossDupFlag)
                && message.Header.GetBoolean(Tags.PossDupFlag),

            PossResend = message.Header.IsSetField(Tags.PossResend)
                && message.Header.GetBoolean(Tags.PossResend),

            SendingTime = message.Header.GetString(Tags.SendingTime),

            OrigSendingTime = message.Header.IsSetField(Tags.OrigSendingTime)
                ? message.Header.GetString(Tags.OrigSendingTime)
                : null,            
        };

            // Queue the message for background processing.
            _messageQueue
                .EnqueueAsync(incomingMessage)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            _logger.LogInformation(
                "Incoming FIX message queued. MsgType: {MsgType}, Session: {SessionId}",
                msgType,
                sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to queue incoming FIX message. Session: {SessionId}",
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

    public void SendToExchange(Message message)
{
    var sessionId = ActiveSession;

    if (!_isLoggedOn || sessionId == null)
    {
        throw new InvalidOperationException(
            "Cannot send FIX message. CHSE FIX session is not logged on.");
    }

    bool sent = Session.SendToTarget(message, sessionId);

    if (!sent)
    {
        throw new InvalidOperationException(
            $"QuickFIX/n could not send the message to session {sessionId}.");
    }

    _logger.LogInformation(
        "Outgoing FIX message submitted to session {SessionId}.",
        sessionId);
}
}