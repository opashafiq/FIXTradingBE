
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.DTOs.Orders;
using TradingBackend.Api.Fix;
using TradingBackend.Api.Entities;
using TradingBackend.Api.Data;
using TradingBackend.Api.Identity;

namespace TradingBackend.Api.Controllers;

[Authorize(Roles = "FirmAdmin,Trader")]
[ApiController]
[Route("api/fix-order-test")]
public class FixOrderTestController : ControllerBase
{
    private readonly FixOrderSubmissionService _submissionService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TradingDbContext _dbContext;

    public FixOrderTestController(
        FixOrderSubmissionService submissionService,
        UserManager<ApplicationUser> userManager,
        TradingDbContext dbContext)
    {
        _submissionService = submissionService;
        _userManager = userManager;
        _dbContext = dbContext;
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] CreateOrderDto dto)
    {
        try
        {
            // Get the authenticated user
            var user = await _userManager.GetUserAsync(User);

            if (user == null || !user.IsActive)
                return Unauthorized();

            // The user must belong to a firm
            if (user.FirmId == null)
                return Forbid();

            // Get the user's firm from the database
            var firm = await _dbContext.Firms
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == user.FirmId &&
                    x.IsActive);

            if (firm == null)
                return Forbid();

            // Get the CHSE workstation ID from the user record
            if (string.IsNullOrWhiteSpace(user.CHSEWorkstationId))
                return BadRequest(new
                {
                    Success = false,
                    Error = "CHSE Workstation ID is not configured for this user."
                });

            // Generate FIX preview using database identities
            string preview = _submissionService.PreviewNewOrderSingle(
                dto,
                firm.CHSEFirmId,
                user.CHSEWorkstationId);

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
