
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

            case "j":
                // BusinessMessageReject is parsed and persisted by FixMessageProcessor.
                break;  

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

    


    public BusinessMessageRejectDto ParseBusinessMessageReject(
        Message message,
        SessionID sessionId)
    {
        var reject = new BusinessMessageRejectDto
        {
            SessionId = sessionId.ToString(),
            BusinessRejectRefID = ReadField(message, 379),
            RefSeqNum = int.TryParse(ReadField(message, 45), out var seqNum)
                ? seqNum
                : null,
            RefMsgType = ReadField(message, 372),
            RefTagID = int.TryParse(ReadField(message, 371), out var tagId)
                ? tagId
                : null,
            BusinessRejectReason = int.TryParse(
                ReadField(message, 380),
                out var reason)
                    ? reason
                    : null,
            Text = ReadField(message, 58)
        };

        _logger.LogWarning(
            "BusinessMessageReject parsed. Session: {SessionId}, " +
            "BusinessRejectRefID: {BusinessRejectRefID}, " +
            "RefSeqNum: {RefSeqNum}, RefMsgType: {RefMsgType}, " +
            "RefTagID: {RefTagID}, Reason: {Reason}, Text: {Text}",
            sessionId,
            reject.BusinessRejectRefID,
            reject.RefSeqNum,
            reject.RefMsgType,
            reject.RefTagID,
            reject.BusinessRejectReason,
            reject.Text);

        return reject;
    }

    public OrderCancelRejectDto ParseOrderCancelReject(
        Message message,
        SessionID sessionId)
    {
        return new OrderCancelRejectDto
        {
            SessionId = sessionId.ToString(),

            OrderId = ReadField(message, 37),
            ClOrdId = ReadField(message, 11),
            OrigClOrdId = ReadField(message, 41),
            OrdStatus = ReadField(message, 39),

            CxlRejResponseTo = ReadIntField(message, 434),
            CxlRejReason = ReadIntField(message, 102),

            Text = ReadField(message, 58)
        };
    }

public SessionRejectDto ParseSessionReject(
    Message message,
    SessionID sessionId)
{
    var reject = new SessionRejectDto
    {
        SessionId = sessionId.ToString(),

        RefSeqNum = ReadIntField(message, 45),
        RefTagID = ReadField(message, 371),
        RefMsgType = ReadField(message, 372),
        SessionRejectReason = ReadIntField(message, 373),
        Text = ReadField(message, 58)
    };

    _logger.LogWarning(
        "SessionReject parsed. Session: {SessionId}, " +
        "RefSeqNum: {RefSeqNum}, RefTagID: {RefTagID}, " +
        "RefMsgType: {RefMsgType}, Reason: {Reason}, Text: {Text}",
        sessionId,
        reject.RefSeqNum,
        reject.RefTagID,
        reject.RefMsgType,
        reject.SessionRejectReason,
        reject.Text);

    return reject;
}

    private static int? ReadIntField(Message message, int tag)
    {
        var value = ReadField(message, tag);

        return int.TryParse(value, out var result)
            ? result
            : null;
    }

}
