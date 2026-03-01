using System.Net;
using System.Net.Http.Json;
using Snakk.Application.DTOs.Management;
using Snakk.Shared.Enums;

namespace Snakk.AdminWeb.Services;

public class ManageScopeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ManageScopeService> _logger;

    public ManageScopeService(HttpClient httpClient, ILogger<ManageScopeService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ManageScopeDto?> ResolveScopeAsync(string communitySlug, string? hubSlug = null, string? spaceSlug = null)
    {
        try
        {
            var query = $"?communitySlug={Uri.EscapeDataString(communitySlug)}";
            if (!string.IsNullOrEmpty(hubSlug))
                query += $"&hubSlug={Uri.EscapeDataString(hubSlug)}";
            if (!string.IsNullOrEmpty(spaceSlug))
                query += $"&spaceSlug={Uri.EscapeDataString(spaceSlug)}";

            var response = await _httpClient.GetAsync($"/manage/resolve{query}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Manage scope not found: community={Community}, hub={Hub}, space={Space}",
                    communitySlug, hubSlug, spaceSlug);
                return null;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Access denied to manage scope: community={Community}, hub={Hub}, space={Space}",
                    communitySlug, hubSlug, spaceSlug);
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ManageScopeDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error resolving manage scope");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving manage scope");
            return null;
        }
    }

    /// <summary>
    /// Builds the base manage URL for the given scope.
    /// </summary>
    public static string GetManageUrl(ManageScopeDto scope)
    {
        return scope.ScopeType switch
        {
            "Community" => $"/c/{scope.CommunitySlug}/manage",
            "Hub" => $"/c/{scope.CommunitySlug}/h/{scope.HubSlug}/manage",
            "Space" => $"/c/{scope.CommunitySlug}/h/{scope.HubSlug}/s/{scope.SpaceSlug}/manage",
            _ => "/admin"
        };
    }

    /// <summary>
    /// Builds the "back to site" URL for the given scope.
    /// </summary>
    public static string GetSiteUrl(ManageScopeDto scope)
    {
        return scope.ScopeType switch
        {
            "Community" => $"/c/{scope.CommunitySlug}",
            "Hub" => $"/c/{scope.CommunitySlug}/h/{scope.HubSlug}",
            "Space" => $"/c/{scope.CommunitySlug}/h/{scope.HubSlug}/{scope.SpaceSlug}",
            _ => "/"
        };
    }

    /// <summary>
    /// Checks if the scope has a specific permission.
    /// </summary>
    public static bool HasPermission(ManageScopeDto scope, ManagePermissionEnum permission)
    {
        return scope.Permissions.Contains(permission);
    }
}
