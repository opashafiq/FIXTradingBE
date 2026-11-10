namespace TradingBackend.Api.Configuration;

public static class OrderSourceConstants
{
    public const string DealerTerminal = "DLR";
    public const string WebTerminal = "WEB";
    public const string IPhone = "IPH";
    public const string IPad = "IPD";
    public const string Android = "ANR";
    public const string AnyDevice = "MOB";

    public static readonly string[] ValidValues =
    {
        DealerTerminal,
        WebTerminal,
        IPhone,
        IPad,
        Android,
        AnyDevice
    };

    public static bool IsValid(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               ValidValues.Contains(value);
    }
}