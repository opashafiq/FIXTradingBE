using Microsoft.AspNetCore.Mvc;
using TradingBackend.Api.DTOs.Orders;
using TradingBackend.Api.Fix;

namespace TradingBackend.Api.Controllers;

[ApiController]
[Route("api/fix-order-test")]
public class FixOrderTestController : ControllerBase
{
    private readonly FixOrderSubmissionService _submissionService;

    public FixOrderTestController(
        FixOrderSubmissionService submissionService)
    {
        _submissionService = submissionService;
    }

    [HttpPost("preview")]
    public IActionResult Preview([FromBody] CreateOrderDto dto)
    {
        try
        {
            string preview =
                _submissionService.PreviewNewOrderSingle(dto);

            return Ok(new
            {
                Success = true,
                Message = preview.Replace('\u0001', '|')
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                Success = false,
                Error = ex.Message
            });
        }
    }
}