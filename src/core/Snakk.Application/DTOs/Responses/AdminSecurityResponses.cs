namespace Snakk.Application.DTOs.Responses;

public record FailedLoginsResponse(
    IEnumerable<object> Failures,
    IEnumerable<SuspiciousIpResponse> SuspiciousIps,
    int Total,
    int Page,
    int PageSize);

public record SuspiciousIpResponse(string IpAddress, int FailureCount, DateTime LastAttempt);

public record ActiveAdminSessionsResponse(IEnumerable<object> Sessions, int Total);

public record SuspiciousActivitiesResponse(IEnumerable<object> Activities, int Total);

public record UserDataExportStatusResponse(string ExportId, string Status, string Message);

public record AdminPermissionsListResponse(IEnumerable<object> Permissions, int Total);

public record AdminRolePermissionsResponse(int RoleId, IEnumerable<object> Permissions, int Total);

public record AdminPermissionActionResponse(string Message, int RoleId, int PermissionId);

public record AdminTemporaryRolesResponse(IEnumerable<object> Elevations, int Total);

public record AdminTemporaryRoleGrantResponse(string Message, object Elevation);

public record AdminTemporaryRoleRevokeResponse(string Message, string ElevationId);
