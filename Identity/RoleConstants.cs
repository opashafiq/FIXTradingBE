namespace TradingBackend.Api.Identity;

public static class RoleConstants
{
    public const string SystemAdmin = "SystemAdmin";
    public const string FirmAdmin = "FirmAdmin";
    public const string Trader = "Trader";

    public static readonly string[] AllRoles =
    {
        SystemAdmin,
        FirmAdmin,
        Trader
    };
}