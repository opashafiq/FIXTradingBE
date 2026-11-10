using Microsoft.AspNetCore.Identity;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Identity;

public class ApplicationUser : IdentityUser
{
    // Null for SystemAdmin.
    // Required for FirmAdmin and Trader.
    public int? FirmId { get; set; }

    public Firm? Firm { get; set; }
    
    // CHSE workstation/trader ID.
    // Required for Trader; normally null for administrators.
    public string? CHSEWorkstationId { get; set; }
    public bool IsActive { get; set; } = true;

}