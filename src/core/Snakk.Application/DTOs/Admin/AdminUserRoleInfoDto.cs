namespace Snakk.Application.DTOs.Admin;

public class AdminUserRoleInfoDto
{
    /// <summary>Database integer PK of the user.</summary>
    public int DbId { get; init; }

    /// <summary>
    /// PublicId of the active platform-wide role (no community/hub/space scope, not revoked),
    /// or null if the user has no platform-wide role.
    /// </summary>
    public string? ActivePlatformRolePublicId { get; init; }

    /// <summary>
    /// The RoleId (integer) of the active platform-wide role, or null.
    /// </summary>
    public int? ActivePlatformRoleId { get; init; }
}
