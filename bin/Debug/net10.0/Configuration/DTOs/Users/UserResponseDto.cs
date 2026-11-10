namespace TradingBackend.Api.DTOs.Users;

public class UserResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int? FirmId { get; set; }

    public string? FirmName { get; set; }

    public string? CHSEWorkstationId { get; set; }

    public IList<string> Roles { get; set; } = new List<string>();
}