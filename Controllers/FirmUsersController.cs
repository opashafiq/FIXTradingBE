using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Users;
using TradingBackend.Api.Identity;

namespace TradingBackend.Api.Controllers;

[ApiController]
[Route("api/firm/users")]
[Authorize(Roles = RoleConstants.FirmAdmin)]
public class FirmUsersController : ControllerBase
{
    private readonly TradingDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public FirmUsersController(
        TradingDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // POST: api/firm/users/trader
    [HttpPost("trader")]
    public async Task<IActionResult> CreateTrader(
        [FromBody] CreateTraderDto dto)
    {
        // Get authenticated FirmAdmin
        var firmAdmin = await _userManager.GetUserAsync(User);

        if (firmAdmin == null || !firmAdmin.FirmId.HasValue)
        {
            return Forbid();
        }

        // Get FirmAdmin's firm
        var firm = await _context.Firms
            .FirstOrDefaultAsync(x =>
                x.Id == firmAdmin.FirmId.Value &&
                x.IsActive);

        if (firm == null)
        {
            return BadRequest(new
            {
                message = "Your firm does not exist or is inactive."
            });
        }

        var workstationId = dto.CHSEWorkstationId.Trim();

        // Check workstation ID uniqueness
        var workstationExists = await _userManager.Users
            .AnyAsync(x => x.CHSEWorkstationId == workstationId);

        if (workstationExists)
        {
            return Conflict(new
            {
                message = "This CHSE workstation ID is already assigned."
            });
        }

        // Create Trader
        var trader = new ApplicationUser
        {
            UserName = dto.UserName.Trim(),
            Email = dto.Email.Trim(),
            FirmId = firm.Id,
            CHSEWorkstationId = workstationId,
            EmailConfirmed = false
        };

        var createResult = await _userManager.CreateAsync(
            trader,
            dto.Password);

        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to create trader.",
                errors = createResult.Errors.Select(x => x.Description)
            });
        }

        // Assign Trader role
        var roleResult = await _userManager.AddToRoleAsync(
            trader,
            RoleConstants.Trader);

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(trader);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "Trader was created but role assignment failed.",
                    errors = roleResult.Errors.Select(x => x.Description)
                });
        }

        var response = new UserResponseDto
        {
            Id = trader.Id,
            UserName = trader.UserName!,
            Email = trader.Email!,
            FirmId = firm.Id,
            FirmName = firm.FirmName,
            CHSEWorkstationId = trader.CHSEWorkstationId,
            Roles = new List<string>
            {
                RoleConstants.Trader
            }
        };

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }

    [HttpGet("traders")]
    public async Task<IActionResult> GetTraders()
    {
        var firmAdmin = await _userManager.GetUserAsync(User);

        if (firmAdmin == null || !firmAdmin.FirmId.HasValue)
            return Forbid();

        // Explicitly retrieve the Firm
        var firm = await _context.Firms
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == firmAdmin.FirmId.Value && x.IsActive);

        if (firm == null)
            return Forbid();

        // Retrieve traders belonging to this Firm
        var traders = await _userManager.Users
            .AsNoTracking()
            .Where(x => x.FirmId == firm.Id)
            .Where(x => x.CHSEWorkstationId != null)
            .OrderBy(x => x.UserName)
            .ToListAsync();

        var result = traders.Select(x => new UserResponseDto
        {
            Id = x.Id,
            UserName = x.UserName!,
            Email = x.Email!,
            FirmId = x.FirmId,
            FirmName = firm.FirmName,
            CHSEWorkstationId = x.CHSEWorkstationId,
            Roles = new List<string> { RoleConstants.Trader }
        }).ToList();

        return Ok(result);
    }

    [HttpPut("traders/{userId}/status")]
    public async Task<IActionResult> UpdateTraderStatus(
        string userId,
        [FromBody] UpdateUserStatusDto dto)
    {
        // Get the authenticated FirmAdmin
        var firmAdmin = await _userManager.GetUserAsync(User);

        if (firmAdmin == null || !firmAdmin.FirmId.HasValue)
            return Forbid();

        // Find the target user within the same firm
        var trader = await _userManager.Users
            .FirstOrDefaultAsync(x =>
                x.Id == userId &&
                x.FirmId == firmAdmin.FirmId.Value &&
                x.CHSEWorkstationId != null);

        if (trader == null)
            return NotFound(new { message = "Trader not found." });

        // Verify that the target user actually has the Trader role
        if (!await _userManager.IsInRoleAsync(trader, RoleConstants.Trader))
            return BadRequest(new { message = "The specified user is not a trader." });

        // Update account status
        trader.IsActive = dto.IsActive;

        var updateResult = await _userManager.UpdateAsync(trader);

        if (!updateResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Failed to update trader status.",
                errors = updateResult.Errors.Select(x => x.Description)
            });
        }

        return Ok(new
        {
            message = dto.IsActive
                ? "Trader activated successfully."
                : "Trader deactivated successfully.",
            userName = trader.UserName,
            isActive = trader.IsActive
        });
    }
}