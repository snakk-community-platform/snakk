using Grpc.Core;
using Snakk.Application.DTOs.Management;
using Snakk.Protos.Manage;
using Snakk.Shared.Enums;

namespace Snakk.Admin.Services;

public class ManageScopeService(
    ManageService.ManageServiceClient manageClient,
    ILogger<ManageScopeService> logger)
{
    public async Task<ManageScopeDto?> ResolveScopeAsync(
        string communitySlug,
        string? hubSlug = null,
        string? spaceSlug = null)
    {
        try
        {
            var request = new ResolveScopeRequest
            {
                CommunitySlug = communitySlug
            };

            if (!string.IsNullOrEmpty(hubSlug))
                request.HubSlug = hubSlug;
            if (!string.IsNullOrEmpty(spaceSlug))
                request.SpaceSlug = spaceSlug;

            logger.LogInformation("Resolving scope via gRPC: community={Community}, hub={Hub}, space={Space}",
                communitySlug, hubSlug, spaceSlug);

            var deadline = DateTime.UtcNow.AddSeconds(10);
            var response = await manageClient.ResolveScopeAsync(request, deadline: deadline);

            return new ManageScopeDto
            {
                ScopeType = response.ScopeType,
                ScopePublicId = response.ScopePublicId,
                ScopeName = response.ScopeName,
                ScopeAvatarUrl = response.HasScopeAvatarUrl ? response.ScopeAvatarUrl : null,
                CommunitySlug = response.CommunitySlug,
                HubSlug = response.HasHubSlug ? response.HubSlug : null,
                SpaceSlug = response.HasSpaceSlug ? response.SpaceSlug : null,
                CommunityName = response.CommunityName,
                HubName = response.HasHubName ? response.HubName : null,
                SpaceName = response.HasSpaceName ? response.SpaceName : null,
                CommunityPublicId = response.CommunityPublicId,
                Permissions = response.Permissions
                    .Select(p => (ManagePermissionEnum)p)
                    .ToList()
            };
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.NotFound or StatusCode.PermissionDenied)
        {
            logger.LogWarning("Manage scope resolution failed: {Status} for community={Community}, hub={Hub}, space={Space}",
                ex.Status.Detail, communitySlug, hubSlug, spaceSlug);
            return null;
        }
        catch (RpcException ex)
        {
            logger.LogError("gRPC error resolving manage scope: {Status}", ex.Status);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resolving manage scope");
            return null;
        }
    }

    public static string GetManageUrl(ManageScopeDto scope) =>
        scope.ScopeType switch
        {
            "Community" => $"c/{scope.CommunitySlug}",
            "Hub" => $"c/{scope.CommunitySlug}/h/{scope.HubSlug}",
            "Space" => $"c/{scope.CommunitySlug}/h/{scope.HubSlug}/s/{scope.SpaceSlug}",
            _ => ""
        };

    public static string GetSiteUrl(ManageScopeDto scope, bool isMultiCommunity) =>
        isMultiCommunity
            ? scope.ScopeType switch
            {
                "Community" => $"/c/{scope.CommunitySlug}",
                "Hub" => $"/c/{scope.CommunitySlug}/h/{scope.HubSlug}",
                "Space" => $"/c/{scope.CommunitySlug}/h/{scope.HubSlug}/{scope.SpaceSlug}",
                _ => "/"
            }
            : scope.ScopeType switch
            {
                "Hub" => $"/h/{scope.HubSlug}",
                "Space" => $"/h/{scope.HubSlug}/{scope.SpaceSlug}",
                _ => "/"
            };

    public static bool HasPermission(ManageScopeDto scope, ManagePermissionEnum permission) =>
        scope.Permissions.Contains(permission);
}
