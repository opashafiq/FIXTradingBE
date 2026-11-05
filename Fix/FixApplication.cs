using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;

namespace TradingBackend.Api.Fix;

public class FixApplication : IApplication
{
    private readonly ILogger<FixApplication> _logger;
    private volatile bool _isLoggedOn;
    private string? _sessionId;

    public bool IsLoggedOn => _isLoggedOn;
    public string? SessionId => _sessionId;

    public FixApplication(ILogger<FixApplication> logger)
    {
        _logger = logger;
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
            "Successfully logged on to FIX session: {SessionId}", sessionId);
    }

    public void OnLogout(SessionID sessionId)
    {
        _isLoggedOn = false;
        _logger.LogWarning(
            "FIX session logged out: {SessionId}", sessionId);
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
        _logger.LogInformation(
            "Application message received from CHSE. Session: {SessionId}",
            sessionId);

        // We will process order acknowledgements,
        // executions and other application messages here.
    }

    public void ToApp(Message message, SessionID sessionId)
    {
        // Outgoing application messages:
        // NewOrderSingle, OrderCancelRequest, etc.
    }
}