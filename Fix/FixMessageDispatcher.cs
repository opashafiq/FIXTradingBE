
using System.Globalization;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using TradingBackend.Api.DTOs.Orders;
using TradingBackend.Api.DTOs.Quotes;

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

    private static int? ReadIntField(Message message, int tag)
    {
        var value = ReadField(message, tag);

        return int.TryParse(value, out var result)
            ? result
            : null;
    }

    public QuoteStatusReportDto ParseQuoteStatusReport(
        Message message,
        SessionID sessionId)
    {
        string? quoteStatus = ReadField(message, 297);
        string? transactTimeValue = ReadField(message, 60);
        string? orderBookValue = ReadField(message, 30001);
        string? rejectReason = ReadField(message, 300);

        if (string.IsNullOrWhiteSpace(quoteStatus))
        {
            throw new FormatException(
                "QuoteStatus Report is missing required tag 297 (QuoteStatus).");
        }

        if (!int.TryParse(orderBookValue, out int orderBook))
        {
            throw new FormatException(
                "QuoteStatus Report has a missing or invalid tag 30001 (OrderBook).");
        }

        if (string.IsNullOrWhiteSpace(transactTimeValue))
        {
            throw new FormatException(
                "QuoteStatus Report is missing required tag 60 (TransactTime).");
        }

        if (quoteStatus == "5" &&
            string.IsNullOrWhiteSpace(rejectReason))
        {
            throw new FormatException(
                "Rejected QuoteStatus Report (297=5) must contain tag 300 (QuoteRejectReason).");
        }

        string[] transactTimeFormats =
        {
            "yyyyMMdd-HH:mm:ss.FFFFFFF",
            "yyyyMMdd-HH:mm:ss"
        };

        if (!DateTimeOffset.TryParseExact(
                transactTimeValue,
                transactTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset transactTime))
        {
            throw new FormatException(
                $"QuoteStatus Report contains an invalid TransactTime: {transactTimeValue}");
        }

        var report = new QuoteStatusReportDto
        {
            SessionId = sessionId.ToString(),

            ApplID = ReadField(message, 1180),
            QuoteMsgID = ReadField(message, 1166),
            BidID = ReadField(message, 30007),
            OfferID = ReadField(message, 30008),

            QuoteStatus = quoteStatus,
            QuoteRejectReason = rejectReason,
            Text = ReadField(message, 58),

            TransactTime = transactTime,
            OrderBook = orderBook
        };

        _logger.LogInformation(
            "QuoteStatusReport parsed. Session: {SessionId}, " +
            "QuoteMsgID: {QuoteMsgID}, QuoteStatus: {QuoteStatus}, " +
            "OrderBook: {OrderBook}, RejectReason: {RejectReason}",
            report.SessionId,
            report.QuoteMsgID,
            report.QuoteStatus,
            report.OrderBook,
            report.QuoteRejectReason);

        return report;
    }

    private void HandleOrderCancelReject(
        Message message,
        SessionID sessionId)
    {
        _logger.LogInformation(
            "OrderCancelReject received from CHSE.");
    }

    private void HandleMassQuoteAcknowledgement(
        Message message,
        SessionID sessionId)
    {
        _logger.LogInformation(
            "MassQuoteAcknowledgement received from CHSE.");
    }

    private void HandleQuoteStatusReport(
        Message message,
        SessionID sessionId)
    {
        _logger.LogInformation(
            "QuoteStatusReport received from CHSE.");
    }

    private void HandleQuoteRequestReject(
        Message message,
        SessionID sessionId)
    {
        _logger.LogInformation(
            "QuoteRequestReject received from CHSE.");
    }

    private void HandleOrderMassCancelReport(
        Message message,
        SessionID sessionId)
    {
        _logger.LogInformation(
            "OrderMassCancelReport received from CHSE.");
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
}
