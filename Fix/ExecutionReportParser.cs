
using System.Globalization;
using QuickFix;
using TradingBackend.Api.DTOs.Orders;

namespace TradingBackend.Api.Fix;

public class ExecutionReportParser
{
    public ExecutionReportDto Parse(
        Message message,
        SessionID sessionId)
    {
        return new ExecutionReportDto
        {
            SessionId = sessionId.ToString(),

            ApplId = ReadString(message, 1180),
            ExecId = ReadString(message, 17),
            ClOrdId = ReadString(message, 11),
            OrigClOrdId = ReadString(message, 41),
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

            TransactTime = ReadDateTime(message, 60),

            ExecRefId = ReadString(message, 19),
            Text = ReadString(message, 58)
        };
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
}
