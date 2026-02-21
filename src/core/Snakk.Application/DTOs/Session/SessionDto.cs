namespace Snakk.Application.DTOs.Session;

public class SessionDto
{
    public required string Id { get; set; }
    public required string IpAddress { get; set; }
    public required string UserAgent { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime ExpiresAt { get; set; }
    public bool IsCurrent { get; set; }
}
