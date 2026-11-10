namespace TradingBackend.Api.DTOs.Firms;

public class FirmResponseDto
{
    public int Id { get; set; }

    public string FirmName { get; set; } = string.Empty;

    public string CHSEFirmId { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}