
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using TradingBackend.Api.DTOs.Orders;

namespace TradingBackend.Api.Fix;

public class FixMessageDispatcher
{
    private readonly ILogger<FixMessageDispatcher> _logger;
    private readonly ExecutionReportParser _executionReportParser;

    public FixMessageDispatcher(
        ILogger<FixMessageDispatcher> logger,
        ExecutionReportParser executionReportParser)
    {
        _logger = logger;
        _executionReportParser = executionReportParser;
    }

    public ExecutionReportDto? Dispatch(Message message, SessionID sessionId)
    {
        string msgType = message.Header.GetString(Tags.MsgType);

        _logger.LogInformation(
            "Incoming FIX message. MsgType: {MsgType}, Session: {SessionId}",
            msgType,
            sessionId);

        switch (msgType)
        {
            case "8":
                return HandleExecutionReport(message, sessionId);

            case "9":
                HandleOrderCancelReject(message, sessionId);
                break;

            case "b":
                HandleMassQuoteAcknowledgement(message, sessionId);
                break;

            case "AI":
                HandleQuoteStatusReport(message, sessionId);
                break;

            case "AG":
                HandleQuoteRequestReject(message, sessionId);
                break;

            case "r":
                HandleOrderMassCancelReport(message, sessionId);
                break;

            default:
                _logger.LogInformation(
                    "No specific handler registered for FIX MsgType: {MsgType}",
                    msgType);
                break;
        }

        return null;
    }


    private ExecutionReportDto HandleExecutionReport(
        Message message,
        SessionID sessionId)
    {
        try
        {
            var report = _executionReportParser.Parse(message, sessionId);

            _logger.LogInformation(
                "ExecutionReport parsed. ExecID: {ExecId}, ClOrdID: {ClOrdId}, " +
                "ExecType: {ExecType}, OrdStatus: {OrdStatus}",
                report.ExecId,
                report.ClOrdId,
                report.ExecType,
                report.OrdStatus);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to parse CHSE ExecutionReport. Session: {SessionId}",
                sessionId);

            throw;
        }
    }

private static string? ReadField(Message message, int tag)
{
    return message.IsSetField(tag)
        ? message.GetString(tag)
        : null;
}


    private void HandleOrderCancelReject(
        Message message,
        SessionID sessionId)
    {
        _logger.LogInformation(
            "OrderCancelReject received from CHSE."
        );
    }

    private void HandleMassQuoteAcknowledgement(
        Message message,
        SessionID sessionId)
    {
        _logger.LogInformation(
            "MassQuoteAcknowledgement received from CHSE."
        );
    }

    private void HandleQuoteStatusReport(
        Message message,
        SessionID sessionId)
    {
        _logger.LogInformation(
            "QuoteStatusReport received from CHSE."
        );
    }

    private void HandleQuoteRequestReject(
        Message message,
        SessionID sessionId)
    {
        _logger.LogInformation(
            "QuoteRequestReject received from CHSE."
        );
    }

    private void HandleOrderMassCancelReport(
        Message message,
        SessionID sessionId)
    {
        _logger.LogInformation(
            "OrderMassCancelReport received from CHSE."
        );
    }
}
