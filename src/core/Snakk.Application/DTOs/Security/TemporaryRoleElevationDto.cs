namespace Snakk.Application.DTOs.Security;

public class TemporaryRoleElevationDto
{
    public required string PublicId { get; set; }
    public required string UserPublicId { get; set; }
    public required string UserDisplayName { get; set; }
    public required string RoleType { get; set; }
    public required string Scope { get; set; }
    public required string ScopePublicId { get; set; }
    public required DateTime ExpiresAt { get; set; }
    public string? Reason { get; set; }
    public required string GrantedByDisplayName { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByDisplayName { get; set; }
    public string? RevokedReason { get; set; }
    public required DateTime CreatedAt { get; set; }

    // Computed properties
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
    public bool IsExpired => RevokedAt is null && ExpiresAt <= DateTime.UtcNow;
}
