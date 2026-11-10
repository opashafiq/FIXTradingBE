using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Users;
using TradingBackend.Api.Identity;

namespace TradingBackend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleConstants.SystemAdmin)]
public class UsersController : ControllerBase
{
    private readonly TradingDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(
        TradingDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // POST: api/users/firm-admin
    [HttpPost("firm-admin")]
    public async Task<IActionResult> CreateFirmAdmin(
        [FromBody] CreateFirmAdminDto dto)
    {
        // Validate FirmId
        if (dto.FirmId <= 0)
        {
            return BadRequest(new
            {
                message = "A valid Firm ID is required."
            });
        }

        // Check whether the firm exists and is active
        var firm = await _context.Firms
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == dto.FirmId && x.IsActive);

        if (firm == null)
        {
            return BadRequest(new
            {
                message = "The selected firm does not exist or is inactive."
            });
        }

        // Create Identity user
        var user = new ApplicationUser
        {
            UserName = dto.UserName.Trim(),
            Email = dto.Email.Trim(),
            FirmId = firm.Id,
            CHSEWorkstationId = null,
            EmailConfirmed = false
        };

        var createResult = await _userManager.CreateAsync(
            user,
            dto.Password);

        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to create user.",
                errors = createResult.Errors.Select(x => x.Description)
            });
        }

        // Assign FirmAdmin role
        var roleResult = await _userManager.AddToRoleAsync(
            user,
            RoleConstants.FirmAdmin);

        if (!roleResult.Succeeded)
        {
            // Roll back user creation if role assignment fails
            await _userManager.DeleteAsync(user);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "User was created but role assignment failed.",
                    errors = roleResult.Errors.Select(x => x.Description)
                });
        }

        // Return response
        var response = new UserResponseDto
        {
            Id = user.Id,
            UserName = user.UserName!,
            Email = user.Email!,
            FirmId = firm.Id,
            FirmName = firm.FirmName,
            CHSEWorkstationId = null,
            Roles = new List<string>
            {
                RoleConstants.FirmAdmin
            }
        };

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }
}