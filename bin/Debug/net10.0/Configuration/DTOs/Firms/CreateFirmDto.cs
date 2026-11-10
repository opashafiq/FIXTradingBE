using System.ComponentModel.DataAnnotations;

namespace TradingBackend.Api.DTOs.Firms;

public class CreateFirmDto
{
    [Required]
    [StringLength(200)]
    public string FirmName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string CHSEFirmId { get; set; } = string.Empty;
}