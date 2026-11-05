using Microsoft.Extensions.Logging;
using QuickFix;

namespace TradingBackend.Api.Fix;

public class FixApplication : IApplication
{
    private readonly ILogger<FixApplication> _logger;

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
        _logger.LogInformation(
            "Successfully logged on to FIX session: {SessionId}", sessionId);
    }

    public void OnLogout(SessionID sessionId)
    {
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
        // Outgoing administrative messages.
        // We will add the CHSE Logon password here in a later step.
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