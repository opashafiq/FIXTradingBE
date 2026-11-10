using System.ComponentModel.DataAnnotations;

namespace TradingBackend.Api.DTOs.Users;

public class CreateTraderDto
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
    [StringLength(50)]
    public string CHSEWorkstationId { get; set; } = string.Empty;
}