namespace Snakk.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Snakk.Application.Services;

public static class AdminPermissionsEndpoints
{
    public static void MapAdminPermissionsEndpoints(this IEndpointRouteBuilder app)
    {
        var permissionsGroup = app.MapGroup("/admin/permissions")
            .WithTags("Admin Permissions")
            .RequireAuthorization();

        // Permission endpoints
        permissionsGroup.MapGet("", GetAllPermissionsAsync)
            .WithName("GetAllPermissions");

        permissionsGroup.MapGet("/roles/{roleId:int}", GetRolePermissionsAsync)
            .WithName("GetRolePermissions");

        permissionsGroup.MapPost("/roles/{roleId:int}", AssignPermissionToRoleAsync)
            .WithName("AssignPermissionToRole");

        permissionsGroup.MapDelete("/roles/{roleId:int}/{permissionId:int}", RevokePermissionFromRoleAsync)
            .WithName("RevokePermissionFromRole");

        // Temporary role endpoints
        var tempRolesGroup = app.MapGroup("/admin/temporary-roles")
            .WithTags("Admin Temporary Roles")
            .RequireAuthorization();

        tempRolesGroup.MapGet("", GetActiveTemporaryRolesAsync)
            .WithName("GetActiveTemporaryRoles");

        tempRolesGroup.MapPost("", GrantTemporaryRoleAsync)
            .WithName("GrantTemporaryRole");

        tempRolesGroup.MapDelete("/{elevationId}", RevokeTemporaryRoleAsync)
            .WithName("RevokeTemporaryRole");
    }

    private static async Task<IResult> GetAllPermissionsAsync(
        IPermissionService permissionService)
    {
        var permissions = await permissionService.GetAllPermissionsAsync();

        return Results.Ok(new
        {
            permissions,
            total = permissions.Count
        });
    }

    private static async Task<IResult> GetRolePermissionsAsync(
        int roleId,
        IPermissionService permissionService)
    {
        var permissions = await permissionService.GetRolePermissionsAsync(roleId);

        return Results.Ok(new
        {
            roleId,
            permissions,
            total = permissions.Count
        });
    }

    private static async Task<IResult> AssignPermissionToRoleAsync(
        int roleId,
        [FromBody] AssignPermissionRequest request,
        IPermissionService permissionService,
        ISecurityService securityService,
        HttpContext httpContext,
        ILogger<object> logger)
    {
        var adminUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminUserId))
            return Results.Unauthorized();

        try
        {
            await permissionService.AssignPermissionToRoleAsync(roleId, request.PermissionId, adminUserId);

            logger.LogInformation("Admin {AdminUserId} assigned permission {PermissionId} to role {RoleId}",
                adminUserId, request.PermissionId, roleId);

            return Results.Ok(new
            {
                message = "Permission assigned successfully",
                roleId,
                permissionId = request.PermissionId
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error assigning permission {PermissionId} to role {RoleId}",
                request.PermissionId, roleId);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> RevokePermissionFromRoleAsync(
        int roleId,
        int permissionId,
        IPermissionService permissionService,
        ISecurityService securityService,
        HttpContext httpContext,
        ILogger<object> logger)
    {
        var adminUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminUserId))
            return Results.Unauthorized();

        try
        {
            await permissionService.RevokePermissionFromRoleAsync(roleId, permissionId, adminUserId);

            logger.LogInformation("Admin {AdminUserId} revoked permission {PermissionId} from role {RoleId}",
                adminUserId, permissionId, roleId);

            return Results.Ok(new
            {
                message = "Permission revoked successfully",
                roleId,
                permissionId
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error revoking permission {PermissionId} from role {RoleId}",
                permissionId, roleId);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetActiveTemporaryRolesAsync(
        IPermissionService permissionService)
    {
        var elevations = await permissionService.GetActiveTemporaryRolesAsync();

        return Results.Ok(new
        {
            elevations,
            total = elevations.Count
        });
    }

    private static async Task<IResult> GrantTemporaryRoleAsync(
        [FromBody] GrantTemporaryRoleRequest request,
        IPermissionService permissionService,
        ISecurityService securityService,
        HttpContext httpContext,
        ILogger<object> logger)
    {
        var adminUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminUserId))
            return Results.Unauthorized();

        try
        {
            var elevation = await permissionService.GrantTemporaryRoleAsync(
                request.UserId,
                request.RoleType,
                request.Scope,
                request.ScopeId,
                request.ExpiresAt,
                request.Reason,
                adminUserId);

            logger.LogInformation(
                "Admin {AdminUserId} granted temporary {RoleType} role to user {UserId} in {Scope}:{ScopeId}, expires at {ExpiresAt}",
                adminUserId, request.RoleType, request.UserId, request.Scope, request.ScopeId, request.ExpiresAt);

            return Results.Ok(new
            {
                message = "Temporary role granted successfully",
                elevation
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error granting temporary role to user {UserId}", request.UserId);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> RevokeTemporaryRoleAsync(
        string elevationId,
        [FromBody] RevokeTemporaryRoleRequest request,
        IPermissionService permissionService,
        ISecurityService securityService,
        HttpContext httpContext,
        ILogger<object> logger)
    {
        var adminUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminUserId))
            return Results.Unauthorized();

        try
        {
            await permissionService.RevokeTemporaryRoleAsync(elevationId, request.Reason, adminUserId);

            logger.LogInformation("Admin {AdminUserId} revoked temporary role elevation {ElevationId}",
                adminUserId, elevationId);

            return Results.Ok(new
            {
                message = "Temporary role revoked successfully",
                elevationId
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error revoking temporary role elevation {ElevationId}", elevationId);
            return Results.Problem(ex.Message);
        }
    }
}

public record AssignPermissionRequest(int PermissionId);
public record GrantTemporaryRoleRequest(string UserId, string RoleType, string Scope, int ScopeId, DateTime ExpiresAt, string Reason);
public record RevokeTemporaryRoleRequest(string Reason);
