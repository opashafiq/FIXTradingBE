using System.ComponentModel.DataAnnotations;

namespace TradingBackend.Api.DTOs.Users;

public class CreateFirmAdminDto
{
    [Required]
    [StringLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public int FirmId { get; set; }
}