using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Models;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Moderation;

public class RolesModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CommunityId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? HubId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SpaceId { get; set; }

    public IEnumerable<UserRoleDto>? Roles { get; set; }
    public UserProfileDto? UserProfile { get; set; }
    public bool CanAdminister { get; set; }

    [BindProperty]
    public AssignRoleRequest? AssignRequest { get; set; }

    public string GetRoleDisplayName(string role) => role switch
    {
        "GlobalAdmin" => "Global Admin",
        "CommunityAdmin" => "Community Admin",
        "CommunityMod" => "Community Moderator",
        "HubMod" => "Hub Moderator",
        "SpaceMod" => "Space Moderator",
        _ => role
    };

    public string GetRoleBadgeClass(string role) => role switch
    {
        "GlobalAdmin" => "badge-error",
        "CommunityAdmin" => "badge-warning",
        "CommunityMod" => "badge-info",
        "HubMod" => "badge-primary",
        "SpaceMod" => "badge-secondary",
        _ => "badge-ghost"
    };

    public async Task<IActionResult> OnGetAsync()
    {
        CanAdminister = await apiClient.CanAdministerAsync(CommunityId, HubId, SpaceId);
        if (!CanAdminister)
        {
            // Even if can't administer, allow viewing if can moderate
            var canModerate = await apiClient.CanModerateAsync();
            if (!canModerate)
            {
                return RedirectToPage("/Index");
            }
        }

        if (!string.IsNullOrEmpty(UserId))
        {
            UserProfile = await apiClient.GetUserProfileAsync(UserId);
            Roles = await apiClient.GetUserRolesAsync(UserId);
        }
        else if (!string.IsNullOrEmpty(SpaceId))
        {
            Roles = await apiClient.GetRolesForSpaceAsync(SpaceId);
        }
        else if (!string.IsNullOrEmpty(HubId))
        {
            Roles = await apiClient.GetRolesForHubAsync(HubId);
        }
        else if (!string.IsNullOrEmpty(CommunityId))
        {
            Roles = await apiClient.GetRolesForCommunityAsync(CommunityId);
        }
        else
        {
            // Show current user's roles
            Roles = await apiClient.GetMyRolesAsync();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAssignAsync()
    {
        if (AssignRequest == null || string.IsNullOrEmpty(AssignRequest.TargetUserId))
        {
            return RedirectToPage();
        }

        await apiClient.AssignRoleAsync(AssignRequest);
        return RedirectToPage(new { UserId = AssignRequest.TargetUserId });
    }

    public async Task<IActionResult> OnPostRevokeAsync(string roleId, string userId)
    {
        await apiClient.RevokeRoleAsync(roleId);
        return RedirectToPage(new { UserId = userId });
    }
}
