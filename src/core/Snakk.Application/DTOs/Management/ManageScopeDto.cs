using Snakk.Shared.Enums;

namespace Snakk.Application.DTOs.Management;

public class ManageScopeDto
{
    public required string ScopeType { get; set; } // "Community", "Hub", "Space"
    public int ScopeId { get; set; }
    public required string ScopeName { get; set; }
    public string? ScopeAvatarUrl { get; set; }

    // Slug data for URL construction
    public required string CommunitySlug { get; set; }
    public string? HubSlug { get; set; }
    public string? SpaceSlug { get; set; }

    // Breadcrumb data
    public required string CommunityName { get; set; }
    public int CommunityId { get; set; }
    public string? HubName { get; set; }
    public int? HubId { get; set; }
    public string? SpaceName { get; set; }
    public int? SpaceId { get; set; }

    // Permissions for the current user at this scope
    public List<ManagePermissionEnum> Permissions { get; set; } = new();
}
