using Microsoft.AspNetCore.SignalR;
using TradingBackend.Api.Fix;

namespace TradingBackend.Api.Hubs;

public class FixStatusHub : Hub
{
    private readonly FixApplication _fixApplication;

    public FixStatusHub(FixApplication fixApplication)
    {
        _fixApplication = fixApplication;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync(
            "FixConnectionStatusChanged",
            new
            {
                Connected = _fixApplication.IsLoggedOn,
                SessionId = _fixApplication.SessionId
            }
        );

        await base.OnConnectedAsync();
    }
}