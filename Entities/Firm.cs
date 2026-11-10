using TradingBackend.Api.Identity;

namespace TradingBackend.Api.Entities;

public class Firm
{
    public int Id { get; set; }

    public string FirmName { get; set; } = string.Empty;

    // Firm identifier assigned by CHSE
    public string CHSEFirmId { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<ApplicationUser> Users { get; set; }
        = new List<ApplicationUser>();
}