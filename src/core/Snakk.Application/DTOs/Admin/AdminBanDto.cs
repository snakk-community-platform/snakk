namespace Snakk.Application.DTOs.Admin;

public class AdminBanDto
{
    public required string UserId { get; set; }
    public required string UserDisplayName { get; set; }
    public required string Reason { get; set; }
    public required string BannedBy { get; set; }
    public required DateTime BannedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
