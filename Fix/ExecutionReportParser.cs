
using System.Globalization;
using QuickFix;
using TradingBackend.Api.DTOs.Orders;

namespace TradingBackend.Api.Fix;

public class ExecutionReportParser
{
    public ExecutionReportDto Parse(Message message, SessionID sessionId)
{
    var report = new ExecutionReportDto
    {
        SessionId = sessionId.ToString(),

        ApplId = ReadString(message, 1180),
        ExecId = ReadString(message, 17),
        ClOrdId = ReadString(message, 11),
        OrigClOrdId = ReadString(message, 41),

        MdEntryId = ReadString(message, 278),
        OrderId = ReadString(message, 37),

        ExecType = ReadString(message, 150),
        OrdStatus = ReadString(message, 39),

        Symbol = ReadString(message, 55),
        Side = ReadString(message, 54),

        OrderQty = ReadDecimal(message, 38),
        LastQty = ReadDecimal(message, 32),
        LastPx = ReadDecimal(message, 31),

        LeavesQty = ReadDecimal(message, 151),
        CumQty = ReadDecimal(message, 14),
        AvgPx = ReadDecimal(message, 6),

        OrderBook = ReadInt(message, 30001),
        OrderSource = ReadString(message, 30004),
        OrderCapacity = ReadString(message, 528),

        TransactTime = ReadDateTime(message, 60),

        ExecRefId = ReadString(message, 19),
        TrdMatchId = ReadString(message, 880),
        SecondaryTradeId = ReadString(message, 1040),
        ExecRestatementReason = ReadString(message, 378),
        OrdRejReason = ReadString(message, 103),

        Text = ReadString(message, 58),
        OrdType = ReadString(message, 40),

        Account = ReadString(message, 1),
        AccountType = ReadInt(message, 581),

        Yield = ReadDecimal(message, 236),
        ConvertedYield = ReadDecimal(message, 30005),

        StreamText = ReadString(message, 40056),
    };

    ValidateExecutionReport(report);

    return report;
}

    private static string? ReadString(
        Message message,
        int tag)
    {
        return message.IsSetField(tag)
            ? message.GetString(tag)
            : null;
    }

    private static decimal? ReadDecimal(
        Message message,
        int tag)
    {
        var value = ReadString(message, tag);

        if (string.IsNullOrWhiteSpace(value))
            return null;

        return decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : throw new FormatException(
                $"Invalid decimal value '{value}' in FIX tag {tag}.");
    }

    private static int? ReadInt(
        Message message,
        int tag)
    {
        var value = ReadString(message, tag);

        if (string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : throw new FormatException(
                $"Invalid integer value '{value}' in FIX tag {tag}.");
    }

    private static DateTimeOffset? ReadDateTime(
        Message message,
        int tag)
    {
        var value = ReadString(message, tag);

        if (string.IsNullOrWhiteSpace(value))
            return null;

        string[] formats =
        {
            "yyyyMMdd-HH:mm:ss",
            "yyyyMMdd-HH:mm:ss.fff",
            "yyyyMMdd-HH:mm:ss.ffffff",
            "yyyyMMdd-HH:mm:ss.ffffffffff"
        };

        if (DateTime.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result))
        {
            // FIX timestamps are interpreted as UTC here.
            return new DateTimeOffset(
                DateTime.SpecifyKind(result, DateTimeKind.Utc));
        }

        throw new FormatException(
            $"Invalid FIX timestamp '{value}' in tag {tag}.");
    }

    private static void ValidateExecutionReport(
    ExecutionReportDto report)
{
    Require(report.ApplId, nameof(report.ApplId));
    Require(report.ExecId, nameof(report.ExecId));
    Require(report.ClOrdId, nameof(report.ClOrdId));
    Require(report.MdEntryId, nameof(report.MdEntryId));
    Require(report.OrderId, nameof(report.OrderId));

    Require(report.ExecType, nameof(report.ExecType));
    Require(report.OrdStatus, nameof(report.OrdStatus));

    Require(report.Symbol, nameof(report.Symbol));
    Require(report.Side, nameof(report.Side));

    Require(report.OrderQty, nameof(report.OrderQty));
    Require(report.LeavesQty, nameof(report.LeavesQty));
    Require(report.CumQty, nameof(report.CumQty));

    Require(report.OrderBook, nameof(report.OrderBook));
    Require(report.OrderCapacity, nameof(report.OrderCapacity));
    Require(report.TransactTime, nameof(report.TransactTime));
    Require(report.OrdType, nameof(report.OrdType));

    var execType = report.ExecType;

    if (execType is "F" or "G")
    {
        Require(report.LastQty, nameof(report.LastQty));
        Require(report.LastPx, nameof(report.LastPx));
    }

    if (execType is "F" or "G" or "H")
    {
        Require(report.TrdMatchId, nameof(report.TrdMatchId));
        Require(report.SecondaryTradeId, nameof(report.SecondaryTradeId));
    }

    if (execType is "G" or "H")
    {
        Require(report.ExecRefId, nameof(report.ExecRefId));
    }

    if (execType == "D")
    {
        Require(
            report.ExecRestatementReason,
            nameof(report.ExecRestatementReason));
    }

    if (execType == "8")
    {
        Require(report.OrdRejReason, nameof(report.OrdRejReason));
    }
}

private static void Require(
    string? value,
    string fieldName)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"CHSE ExecutionReport missing required field: {fieldName}");
    }
}

private static void Require<T>(
    T? value,
    string fieldName) where T : struct
{
    if (!value.HasValue)
    {
        throw new InvalidOperationException(
            $"CHSE ExecutionReport missing required field: {fieldName}");
    }
}
}
