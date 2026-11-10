using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Firms;
using TradingBackend.Api.Entities;
using TradingBackend.Api.Identity;

namespace TradingBackend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleConstants.SystemAdmin)]
public class FirmsController : ControllerBase
{
    private readonly TradingDbContext _context;

    public FirmsController(TradingDbContext context)
    {
        _context = context;
    }

    // GET: api/firms
    [HttpGet]
    public async Task<IActionResult> GetFirms()
    {
        var firms = await _context.Firms
            .AsNoTracking()
            .OrderBy(x => x.FirmName)
            .Select(x => new FirmResponseDto
            {
                Id = x.Id,
                FirmName = x.FirmName,
                CHSEFirmId = x.CHSEFirmId,
                IsActive = x.IsActive,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(firms);
    }

    // POST: api/firms
    [HttpPost]
    public async Task<IActionResult> CreateFirm(
        [FromBody] CreateFirmDto dto)
    {
        var firmName = dto.FirmName.Trim();
        var chseFirmId = dto.CHSEFirmId.Trim();

        // Check duplicate CHSE Firm ID
        var exists = await _context.Firms
            .AnyAsync(x => x.CHSEFirmId == chseFirmId);

        if (exists)
        {
            return Conflict(new
            {
                message = "This CHSE Firm ID is already registered."
            });
        }

        var firm = new Firm
        {
            FirmName = firmName,
            CHSEFirmId = chseFirmId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Firms.Add(firm);

        await _context.SaveChangesAsync();

        var response = new FirmResponseDto
        {
            Id = firm.Id,
            FirmName = firm.FirmName,
            CHSEFirmId = firm.CHSEFirmId,
            IsActive = firm.IsActive,
            CreatedAtUtc = firm.CreatedAtUtc
        };

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }
}