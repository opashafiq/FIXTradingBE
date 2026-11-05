
using Microsoft.AspNetCore.Mvc;
using TradingBackend.Api.Fix;

namespace TradingBackend.Api.Controllers;

[ApiController]
[Route("api/fix")]
public class FixStatusController : ControllerBase
{
    private readonly FixApplication _fixApplication;

    public FixStatusController(FixApplication fixApplication)
    {
        _fixApplication = fixApplication;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            Connected = _fixApplication.IsLoggedOn,
            SessionId = _fixApplication.SessionId
        });
    }
}
