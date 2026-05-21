namespace Snakk.Application.DTOs.Session;

public class LoginHistoryDto
{
    public required string Id { get; set; }
    public required DateTime CreatedAt { get; set; }
    public bool Success { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceHint { get; set; }
}

public class LoginHistoryListResponse
{
    public required List<LoginHistoryDto> Entries { get; set; }
}
