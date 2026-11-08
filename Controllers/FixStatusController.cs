
using Microsoft.AspNetCore.Mvc;
using TradingBackend.Api.Fix;

namespace TradingBackend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FixStatusController : ControllerBase
{
    private readonly FixApplication _fixApplication;
    private readonly IIncomingFixMessageQueue _messageQueue;

    public FixStatusController(
        FixApplication fixApplication,
        IIncomingFixMessageQueue messageQueue)
    {
        _fixApplication = fixApplication;
        _messageQueue = messageQueue;
    }

    // GET: api/FixStatus
    [HttpGet]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            Connected = _fixApplication.IsLoggedOn,
            SessionId = _fixApplication.SessionId
        });
    }


}
