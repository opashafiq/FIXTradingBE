
using Microsoft.AspNetCore.Mvc;
using TradingBackend.Api.Fix;
using TradingBackend.Api.DTOs.Orders;

namespace TradingBackend.Api.Controllers;

using TradingBackend.Api.Entities;
using TradingBackend.Api.Data;

[ApiController]
[Route("api/[controller]")]
public class FixStatusController : ControllerBase
{
    private readonly FixApplication _fixApplication;
    //private readonly IIncomingFixMessageQueue _messageQueue;
    private readonly ExecutionReportPersistenceService _persistenceService;

    private readonly TradingDbContext _dbContext;
    

public FixStatusController(
    FixApplication fixApplication,
    ExecutionReportPersistenceService persistenceService,
    TradingDbContext dbContext)
{
    _fixApplication = fixApplication;
    _persistenceService = persistenceService;
    _dbContext = dbContext;
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
