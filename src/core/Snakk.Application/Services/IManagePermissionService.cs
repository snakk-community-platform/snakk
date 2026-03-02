using Snakk.Shared.Enums;

namespace Snakk.Application.Services;

/// <summary>
/// Service for determining manage page permissions based on user roles.
/// Derives granular permissions from existing role types.
/// </summary>
public interface IManagePermissionService
{
    /// <summary>
    /// Get the full permission set for a user at a given scope.
    /// </summary>
    Task<ManagePermissionSet> GetPermissionsForScopeAsync(string userId, string scopeType, string scopePublicId);

    /// <summary>
    /// Check if a user has a specific manage permission at a given scope.
    /// </summary>
    Task<bool> HasPermissionAsync(string userId, string scopeType, string scopePublicId, ManagePermissionEnum permission);
}

/// <summary>
/// Represents the set of manage permissions a user has at a specific scope.
/// </summary>
public class ManagePermissionSet
{
    public bool ViewDashboard { get; set; }
    public bool ManageContent { get; set; }
    public bool ManageReports { get; set; }
    public bool ManageBans { get; set; }
    public bool ManageSettings { get; set; }
    public bool ManageTeam { get; set; }
    public bool ManageWebhooks { get; set; }

    /// <summary>
    /// Returns true if the user has at least one permission (can access manage pages at all).
    /// </summary>
    public bool HasAnyPermission =>
        ViewDashboard || ManageContent || ManageReports || ManageBans
        || ManageSettings || ManageTeam || ManageWebhooks;

    /// <summary>
    /// Check a specific permission by enum value.
    /// </summary>
    public bool HasPermission(ManagePermissionEnum permission) => permission switch
    {
        ManagePermissionEnum.ViewDashboard => ViewDashboard,
        ManagePermissionEnum.ManageContent => ManageContent,
        ManagePermissionEnum.ManageReports => ManageReports,
        ManagePermissionEnum.ManageBans => ManageBans,
        ManagePermissionEnum.ManageSettings => ManageSettings,
        ManagePermissionEnum.ManageTeam => ManageTeam,
        ManagePermissionEnum.ManageWebhooks => ManageWebhooks,
        _ => false
    };

    /// <summary>
    /// Returns the list of granted permissions.
    /// </summary>
    public List<ManagePermissionEnum> GetGrantedPermissions()
    {
        var permissions = new List<ManagePermissionEnum>();

        if (ViewDashboard) permissions.Add(ManagePermissionEnum.ViewDashboard);
        if (ManageContent) permissions.Add(ManagePermissionEnum.ManageContent);
        if (ManageReports) permissions.Add(ManagePermissionEnum.ManageReports);
        if (ManageBans) permissions.Add(ManagePermissionEnum.ManageBans);
        if (ManageSettings) permissions.Add(ManagePermissionEnum.ManageSettings);
        if (ManageTeam) permissions.Add(ManagePermissionEnum.ManageTeam);
        if (ManageWebhooks) permissions.Add(ManagePermissionEnum.ManageWebhooks);

        return permissions;
    }

    /// <summary>
    /// Creates a permission set with all permissions granted.
    /// </summary>
    public static ManagePermissionSet All => new()
    {
        ViewDashboard = true,
        ManageContent = true,
        ManageReports = true,
        ManageBans = true,
        ManageSettings = true,
        ManageTeam = true,
        ManageWebhooks = true
    };

    /// <summary>
    /// Creates a permission set for a standard moderator (no settings, team, or webhooks).
    /// </summary>
    public static ManagePermissionSet Moderator => new()
    {
        ViewDashboard = true,
        ManageContent = true,
        ManageReports = true,
        ManageBans = true,
        ManageSettings = false,
        ManageTeam = false,
        ManageWebhooks = false
    };

    /// <summary>
    /// Creates an empty permission set.
    /// </summary>
    public static ManagePermissionSet None => new();
}
