using Grpc.Core;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using Snakk.Protos.Manage;
using Snakk.Shared.Enums;
using Snakk.Shared.Helpers;
using System.Security.Claims;
using AppUpdateRequest = Snakk.Application.DTOs.Management.UpdateSpaceSettingsRequest;
using AppRuleDto = Snakk.Application.DTOs.Management.RuleDto;
using AppUpdateRulesRequest = Snakk.Application.DTOs.Management.UpdateRulesRequest;

namespace Snakk.Api.GrpcServices;

public class ManageGrpcService(
    IManageScopeDataService scopeData,
    IManagePermissionService permissionService,
    ICommunityManagementService communityManagementService,
    ISpaceManagementService spaceManagementService,
    IModerationRepository moderationRepository,
    IDashboardChartRepository dashboardChartRepository,
    BannerUseCase bannerUseCase,
    ISettingsService settingsService,
    IRuleService ruleService,
    IRealtimeNotifier realtimeNotifier,
    IActivityBroadcaster activityBroadcaster,
    IGroupService groupService,
    IGroupAccessService groupAccessService,
    IHubManagementService hubManagementService,
    IAllowedTypesService allowedTypesService,
    DiscussionUseCase discussionUseCase,
    HybridCache cache) : ManageService.ManageServiceBase
{
    public override async Task<ResolveScopeResponse> ResolveScope(
        ResolveScopeRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var community = await scopeData.GetCommunityBySlugAsync(request.CommunitySlug);

        if (community is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        // Community scope
        if (!request.HasHubSlug)
        {
            var permissions = await permissionService.GetPermissionsForScopeAsync(
                userId, "Community", community.PublicId);

            if (!permissions.HasAnyPermission)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

            var communityAvatarUrl = AvatarHelper.GetAvatarUrl(community.PublicId, AvatarEntityType.Community, 0, community.AvatarFileName);
            return BuildResponse("Community", community.PublicId, community.Name,
                community.Slug, null, null,
                community.Name, null, null,
                permissions,
                communityPublicId: community.PublicId,
                scopeAvatarUrl: communityAvatarUrl);
        }

        // Resolve hub
        var hub = await scopeData.GetHubBySlugInCommunityAsync(request.HubSlug, community.DbId);

        if (hub is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Hub not found"));

        // Hub scope
        if (!request.HasSpaceSlug)
        {
            var permissions = await permissionService.GetPermissionsForScopeAsync(
                userId, "Hub", hub.PublicId);

            if (!permissions.HasAnyPermission)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

            var hubAvatarUrl = AvatarHelper.GetAvatarUrl(hub.PublicId, AvatarEntityType.Hub, 0, hub.AvatarFileName);
            return BuildResponse("Hub", hub.PublicId, hub.Name,
                community.Slug, hub.Slug, null,
                community.Name, hub.Name, null,
                permissions,
                communityPublicId: community.PublicId,
                scopeAvatarUrl: hubAvatarUrl);
        }

        // Resolve space
        var space = await scopeData.GetSpaceBySlugInHubAsync(request.SpaceSlug, hub.DbId);

        if (space is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        var spacePermissions = await permissionService.GetPermissionsForScopeAsync(
            userId, "Space", space.PublicId);

        if (!spacePermissions.HasAnyPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var spaceAvatarUrl = AvatarHelper.GetAvatarUrl(space.PublicId, AvatarEntityType.Space, 0, space.AvatarFileName);
        return BuildResponse("Space", space.PublicId, space.Name,
            community.Slug, hub.Slug, space.Slug,
            community.Name, hub.Name, space.Name,
            spacePermissions,
            communityPublicId: community.PublicId,
            scopeAvatarUrl: spaceAvatarUrl);
    }

    public override async Task<SpaceSettingsResponse> GetSpaceSettings(
        GetSpaceSettingsRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, "Space", request.SpacePublicId, ManagePermissionEnum.ManageSettings);

        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var settings = await spaceManagementService.GetSettingsAsync(request.SpacePublicId);
        if (settings is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        var response = new SpaceSettingsResponse
        {
            Slug = settings.Slug,
            Name = settings.Name,
            Description = settings.Description ?? "",
            RequireApproval = settings.RequireApproval,
            AllowAnonymous = settings.AllowAnonymous,
            AutoParagraphEnabled = settings.AutoParagraphEnabled,
            IsAdultOnly = settings.IsAdultOnly,
            AllowsAdultContent = settings.AllowsAdultContent,
            Require2Fa = settings.Require2FA
        };

        if (settings.LanguageCode is not null) response.LanguageCode = settings.LanguageCode;
        if (settings.HubLanguageCode is not null) response.HubLanguageCode = settings.HubLanguageCode;
        if (settings.CommunityLanguageCode is not null) response.CommunityLanguageCode = settings.CommunityLanguageCode;

        response.AllowedDiscussionTypes.AddRange(
            settings.AllowedDiscussionTypes.Select(t => (int)t));
        response.ModeratorUserIds.AddRange(settings.ModeratorUserIds);

        var parentTypes = await allowedTypesService.GetParentAllowedTypesForSpaceAsync(request.SpacePublicId);
        response.ParentAllowedTypes.AddRange(parentTypes.Select(t => (int)t));

        return response;
    }

    public override async Task<UpdateSpaceSettingsResponse> UpdateSpaceSettings(
        UpdateSpaceSettingsRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, "Space", request.SpacePublicId, ManagePermissionEnum.ManageSettings);

        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var updateRequest = new AppUpdateRequest
        {
            Name = request.Name,
            Description = request.HasDescription ? request.Description : null,
            LanguageCode = request.HasLanguageCode ? request.LanguageCode : null,
            AllowedDiscussionTypes = request.AllowedDiscussionTypes
                .Select(t => (DiscussionTypeEnum)t)
                .ToList(),
            RequireApproval = request.RequireApproval,
            AllowAnonymous = request.AllowAnonymous,
            AutoParagraphEnabled = request.AutoParagraphEnabled,
            IsAdultOnly = request.IsAdultOnly,
            AllowsAdultContent = request.AllowsAdultContent,
            Require2FA = request.Require2Fa
        };

        var result = await spaceManagementService.UpdateSettingsAsync(
            request.SpacePublicId, updateRequest);

        if (result is not null)
            await cache.RemoveAsync($"space-meta:{request.SpacePublicId}");

        return new UpdateSpaceSettingsResponse
        {
            Success = result is not null,
            ErrorMessage = result is null ? "Failed to update settings" : ""
        };
    }

    public override async Task<SpaceDiscordSettingsResponse> GetSpaceDiscordSettings(
        GetSpaceDiscordSettingsRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, "Space", request.SpacePublicId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var discord = await scopeData.GetSpaceDiscordSettingsAsync(request.SpacePublicId);

        var response = new SpaceDiscordSettingsResponse();
        if (discord?.DiscordWebhookUrl is not null) response.DiscordWebhookUrl = discord.DiscordWebhookUrl;
        if (discord?.DiscordChannelName is not null) response.DiscordChannelName = discord.DiscordChannelName;
        if (discord?.DiscordInviteUrl is not null) response.DiscordInviteUrl = discord.DiscordInviteUrl;
        return response;
    }

    public override async Task<UpdateSpaceDiscordSettingsResponse> UpdateSpaceDiscordSettings(
        UpdateSpaceDiscordSettingsRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, "Space", request.SpacePublicId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        // Validate URL first, before calling into the service that does the AsTracking mutation.
        string? webhookUrl = null;
        if (request.HasDiscordWebhookUrl && !string.IsNullOrWhiteSpace(request.DiscordWebhookUrl))
        {
            var trimmed = request.DiscordWebhookUrl.Trim();
            if (!IsValidDiscordWebhookUrl(trimmed))
                return new UpdateSpaceDiscordSettingsResponse { Success = false, ErrorMessage = "Invalid Discord webhook URL. Must be an HTTPS discord.com webhook." };
            webhookUrl = trimmed;
        }

        var settings = new Snakk.Application.DTOs.Management.SpaceDiscordSettingsDto
        {
            DiscordWebhookUrl = webhookUrl,
            DiscordChannelName = request.HasDiscordChannelName && !string.IsNullOrWhiteSpace(request.DiscordChannelName)
                ? request.DiscordChannelName.Trim() : null,
            DiscordInviteUrl = request.HasDiscordInviteUrl && !string.IsNullOrWhiteSpace(request.DiscordInviteUrl)
                ? request.DiscordInviteUrl.Trim() : null
        };

        var updated = await scopeData.UpdateSpaceDiscordSettingsAsync(request.SpacePublicId, settings);
        if (!updated)
            return new UpdateSpaceDiscordSettingsResponse { Success = false, ErrorMessage = "Space not found" };

        return new UpdateSpaceDiscordSettingsResponse { Success = true };
    }

    public override async Task<GetRulesResponse> GetRules(
        GetRulesRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        // Site scope requires global admin; other scopes use normal permission check
        if (request.ScopeType == "Site")
        {
            var isGlobalAdmin = await scopeData.IsGlobalAdminAsync(userId);
            if (!isGlobalAdmin)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Global admin access required"));
        }
        else
        {
            var hasPermission = await permissionService.HasPermissionAsync(
                userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ManageContent);

            if (!hasPermission)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
        }

        var response = new GetRulesResponse();

        // Current scope's rules
        var rules = await ruleService.GetRulesAsync(request.ScopeType, request.ScopePublicId);
        response.Rules.AddRange(rules.Rules.Select(r => new RuleItem
            { Title = r.Title, Description = r.Description, Order = r.Order }));

        // Inherited rules from parent scopes (for admin panel display)
        switch (request.ScopeType)
        {
            case "Hub":
            {
                var hubParent = await scopeData.GetHubParentAsync(request.ScopePublicId);

                // We need CommunityHasRules — use GetHubWithCommunityAsync to get full hub+community data
                var hubWithCommunity = await scopeData.GetHubWithCommunityAsync(request.ScopePublicId);
                // GetHubParentAsync already gives us CommunityPublicId + Name; check rules via ruleService
                if (hubParent is not null)
                {
                    var communityRules = await ruleService.GetRulesAsync("Community", hubParent.CommunityPublicId);
                    if (communityRules.Rules.Count > 0)
                        await AddInheritedGroup(response, "Community", hubParent.CommunityName, hubParent.CommunityPublicId);
                }

                break;
            }
            case "Space":
            {
                var spaceParents = await scopeData.GetSpaceParentsAsync(request.ScopePublicId);

                if (spaceParents is not null)
                {
                    var communityRules = await ruleService.GetRulesAsync("Community", spaceParents.CommunityPublicId);
                    if (communityRules.Rules.Count > 0)
                        await AddInheritedGroup(response, "Community", spaceParents.CommunityName, spaceParents.CommunityPublicId);

                    var hubRules = await ruleService.GetRulesAsync("Hub", spaceParents.HubPublicId);
                    if (hubRules.Rules.Count > 0)
                        await AddInheritedGroup(response, "Hub", spaceParents.HubName, spaceParents.HubPublicId);
                }

                break;
            }
        }

        // Add inherited site rules for Community/Hub/Space scopes
        if (request.ScopeType is "Community" or "Hub" or "Space")
        {
            var siteRules = await ruleService.GetRulesAsync("Site", null);
            if (siteRules.Rules.Count > 0)
            {
                var group = new InheritedRuleGroup { SourceScopeType = "Site", SourceScopeName = "Site" };
                group.Rules.AddRange(siteRules.Rules.Select(r => new RuleItem
                    { Title = r.Title, Description = r.Description, Order = r.Order }));
                response.InheritedGroups.Insert(0, group);
            }
        }

        return response;
    }

    private async Task AddInheritedGroup(
        GetRulesResponse response,
        string scopeType,
        string scopeName,
        string scopePublicId)
    {
        var inherited = await ruleService.GetRulesAsync(scopeType, scopePublicId);
        if (inherited.Rules.Count > 0)
        {
            var group = new InheritedRuleGroup { SourceScopeType = scopeType, SourceScopeName = scopeName };
            group.Rules.AddRange(inherited.Rules.Select(r => new RuleItem
                { Title = r.Title, Description = r.Description, Order = r.Order }));
            response.InheritedGroups.Add(group);
        }
    }

    public override async Task<UpdateRulesResponse> UpdateRules(
        UpdateRulesRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        // Site scope requires global admin; other scopes use normal permission check
        if (request.ScopeType == "Site")
        {
            var isGlobalAdmin = await scopeData.IsGlobalAdminAsync(userId);
            if (!isGlobalAdmin)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Global admin access required"));
        }
        else
        {
            var hasPermission = await permissionService.HasPermissionAsync(
                userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ManageContent);

            if (!hasPermission)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
        }

        await ruleService.UpdateRulesAsync(
            request.ScopeType,
            request.ScopePublicId,
            new AppUpdateRulesRequest
            {
                Rules = request.Rules.Select(r => new AppRuleDto
                    { Title = r.Title, Description = r.Description, Order = r.Order }).ToList()
            });

        return new UpdateRulesResponse { Success = true };
    }

    public override async Task<GetReportReasonsResponse> GetReportReasons(
        GetReportReasonsRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ManageContent);

        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var response = new GetReportReasonsResponse();

        // Get custom reasons for this exact scope
        var customReasons = await moderationRepository.GetReportReasonsForExactScopeAsync(
            request.ScopeType, request.ScopePublicId);

        response.Reasons.AddRange(customReasons.Select(r => new ReportReasonItem
            { Name = r.Name, Description = r.Description ?? "", DisplayOrder = r.DisplayOrder }));

        // Get inherited reasons: global + parent scopes
        var globalReasons = await moderationRepository.GetGlobalReportReasonsAsync();
        if (globalReasons.Any())
        {
            var globalGroup = new InheritedReportReasonGroup
                { SourceScopeType = "Global", SourceScopeName = "Platform" };
            globalGroup.Reasons.AddRange(globalReasons.Select(r => new ReportReasonItem
                { Name = r.Name, Description = r.Description ?? "", DisplayOrder = r.DisplayOrder }));
            response.InheritedGroups.Add(globalGroup);
        }

        switch (request.ScopeType)
        {
            case "Hub":
            {
                var hubWithCommunity = await scopeData.GetHubWithCommunityAsync(request.ScopePublicId);

                if (hubWithCommunity is not null)
                {
                    var communityReasons = await moderationRepository.GetReportReasonsForExactScopeAsync(
                        "Community", hubWithCommunity.CommunityPublicId);
                    if (communityReasons.Any())
                    {
                        var group = new InheritedReportReasonGroup
                            { SourceScopeType = "Community", SourceScopeName = hubWithCommunity.CommunityName };
                        group.Reasons.AddRange(communityReasons.Select(r => new ReportReasonItem
                            { Name = r.Name, Description = r.Description ?? "", DisplayOrder = r.DisplayOrder }));
                        response.InheritedGroups.Add(group);
                    }
                }

                break;
            }
            case "Space":
            {
                var spaceWithHubCommunity = await scopeData.GetSpaceWithHubCommunityAsync(request.ScopePublicId);

                if (spaceWithHubCommunity is not null)
                {
                    var communityReasons = await moderationRepository.GetReportReasonsForExactScopeAsync(
                        "Community", spaceWithHubCommunity.CommunityPublicId);
                    if (communityReasons.Any())
                    {
                        var group = new InheritedReportReasonGroup
                            { SourceScopeType = "Community", SourceScopeName = spaceWithHubCommunity.CommunityName };
                        group.Reasons.AddRange(communityReasons.Select(r => new ReportReasonItem
                            { Name = r.Name, Description = r.Description ?? "", DisplayOrder = r.DisplayOrder }));
                        response.InheritedGroups.Add(group);
                    }

                    var hubReasons = await moderationRepository.GetReportReasonsForExactScopeAsync(
                        "Hub", spaceWithHubCommunity.HubPublicId);
                    if (hubReasons.Any())
                    {
                        var group = new InheritedReportReasonGroup
                            { SourceScopeType = "Hub", SourceScopeName = spaceWithHubCommunity.HubName };
                        group.Reasons.AddRange(hubReasons.Select(r => new ReportReasonItem
                            { Name = r.Name, Description = r.Description ?? "", DisplayOrder = r.DisplayOrder }));
                        response.InheritedGroups.Add(group);
                    }
                }

                break;
            }
        }

        return response;
    }

    public override async Task<UpdateReportReasonsResponse> UpdateReportReasons(
        UpdateReportReasonsRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ManageContent);

        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var reasons = request.Reasons
            .Select(r => (r.Name, Description: (string?)r.Description, r.DisplayOrder));

        await moderationRepository.ReplaceReportReasonsForScopeAsync(
            request.ScopeType, request.ScopePublicId, userId, reasons);

        return new UpdateReportReasonsResponse { Success = true };
    }

    // ==================== Bans ====================

    public override async Task<GetBansResponse> GetBans(
        GetBansRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ManageBans);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var bans = await moderationRepository.GetActiveBansForScopeAsync(
            request.ScopeType, request.ScopePublicId);

        var response = new GetBansResponse();
        response.Bans.AddRange(MapBanItems(bans));

        // Inherited bans from parent scopes
        switch (request.ScopeType)
        {
            case "Hub":
            {
                var parent = await scopeData.GetHubParentAsync(request.ScopePublicId);

                if (parent is not null)
                {
                    var communityBans = await moderationRepository.GetActiveBansForScopeAsync(
                        "Community", parent.CommunityPublicId);
                    if (communityBans.Any())
                    {
                        var group = new InheritedBanGroup
                            { SourceScopeType = "Community", SourceScopeName = parent.CommunityName };
                        group.Bans.AddRange(MapBanItems(communityBans));
                        response.InheritedGroups.Add(group);
                    }
                }

                break;
            }
            case "Space":
            {
                var parents = await scopeData.GetSpaceParentsAsync(request.ScopePublicId);

                if (parents is not null)
                {
                    var communityTask = moderationRepository.GetActiveBansForScopeAsync(
                        "Community", parents.CommunityPublicId);
                    var hubTask = moderationRepository.GetActiveBansForScopeAsync(
                        "Hub", parents.HubPublicId);
                    await Task.WhenAll(communityTask, hubTask);

                    var communityBans = await communityTask;
                    if (communityBans.Any())
                    {
                        var group = new InheritedBanGroup
                            { SourceScopeType = "Community", SourceScopeName = parents.CommunityName };
                        group.Bans.AddRange(MapBanItems(communityBans));
                        response.InheritedGroups.Add(group);
                    }

                    var hubBans = await hubTask;
                    if (hubBans.Any())
                    {
                        var group = new InheritedBanGroup
                            { SourceScopeType = "Hub", SourceScopeName = parents.HubName };
                        group.Bans.AddRange(MapBanItems(hubBans));
                        response.InheritedGroups.Add(group);
                    }
                }

                break;
            }
        }

        return response;
    }

    private static IEnumerable<BanItem> MapBanItems(IEnumerable<UserBanDto> bans) =>
        bans.Select(b =>
        {
            var item = new BanItem
            {
                PublicId = b.PublicId,
                UserDisplayName = b.UserDisplayName,
                BanType = b.BanType,
                Reason = b.Reason ?? "",
                BannedByDisplayName = b.BannedByUserDisplayName,
                BannedAtTicks = b.BannedAt.Ticks
            };
            if (b.ExpiresAt.HasValue) item.ExpiresAtTicks = b.ExpiresAt.Value.Ticks;
            return item;
        });

    public override async Task<CreateBanResponse> CreateBan(
        CreateBanRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ManageBans);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        // Find target user by display name
        var targetUser = await scopeData.FindUserPublicIdByDisplayNameAsync(request.TargetUsername);

        if (targetUser is null)
            return new CreateBanResponse { Success = false, ErrorMessage = "User not found" };

        var banType = request.BanType == "WriteOnly" ? BanType.WriteOnly : BanType.ReadWrite;
        DateTime? expiresAt = request.HasExpiresAtTicks
            ? new DateTime(request.ExpiresAtTicks, DateTimeKind.Utc) : null;

        string? communityPublicId = null, hubPublicId = null, spacePublicId = null;
        switch (request.ScopeType)
        {
            case "Community": communityPublicId = request.ScopePublicId; break;
            case "Hub": hubPublicId = request.ScopePublicId; break;
            case "Space": spacePublicId = request.ScopePublicId; break;
        }

        await moderationRepository.BanUserAsync(
            targetUser, banType, communityPublicId, hubPublicId, spacePublicId,
            request.Reason, expiresAt, userId);

        var moderatorName = await scopeData.GetUserDisplayNameByPublicIdAsync(userId) ?? userId;

        await activityBroadcaster.BroadcastUserBanned(
            userId, moderatorName, targetUser, request.TargetUsername, request.Reason);

        return new CreateBanResponse { Success = true };
    }

    public override async Task<RevokeBanResponse> RevokeBan(
        RevokeBanRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        // Permission check happens via the ban's scope - we need to look up the ban first
        var ban = await moderationRepository.GetBanByPublicIdAsync(request.BanPublicId);
        if (ban is null)
            return new RevokeBanResponse { Success = false, ErrorMessage = "Ban not found" };

        // Determine scope from ban for permission check
        var scopeType = ban.SpacePublicId is not null ? "Space"
            : ban.HubPublicId is not null ? "Hub"
            : ban.CommunityPublicId is not null ? "Community" : "Community";
        var scopeId = ban.SpacePublicId ?? ban.HubPublicId ?? ban.CommunityPublicId ?? "";

        if (!string.IsNullOrEmpty(scopeId))
        {
            var hasPermission = await permissionService.HasPermissionAsync(
                userId, scopeType, scopeId, ManagePermissionEnum.ManageBans);
            if (!hasPermission)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
        }

        await moderationRepository.UnbanUserAsync(request.BanPublicId, userId);

        return new RevokeBanResponse { Success = true };
    }

    // ==================== Team ====================

    public override async Task<GetTeamResponse> GetTeam(
        GetTeamRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ManageTeam);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var roles = await moderationRepository.GetActiveRolesForScopeAsync(
            request.ScopeType, request.ScopePublicId);

        var response = new GetTeamResponse();
        response.Members.AddRange(MapTeamItems(roles));

        // Inherited team members from parent scopes
        switch (request.ScopeType)
        {
            case "Hub":
            {
                var parent = await scopeData.GetHubParentAsync(request.ScopePublicId);

                if (parent is not null)
                {
                    var communityRoles = await moderationRepository.GetActiveRolesForScopeAsync(
                        "Community", parent.CommunityPublicId);
                    if (communityRoles.Any())
                    {
                        var group = new InheritedTeamGroup
                            { SourceScopeType = "Community", SourceScopeName = parent.CommunityName };
                        group.Members.AddRange(MapTeamItems(communityRoles));
                        response.InheritedGroups.Add(group);
                    }
                }

                break;
            }
            case "Space":
            {
                var parents = await scopeData.GetSpaceParentsAsync(request.ScopePublicId);

                if (parents is not null)
                {
                    var communityTask = moderationRepository.GetActiveRolesForScopeAsync(
                        "Community", parents.CommunityPublicId);
                    var hubTask = moderationRepository.GetActiveRolesForScopeAsync(
                        "Hub", parents.HubPublicId);
                    await Task.WhenAll(communityTask, hubTask);

                    var communityRoles = await communityTask;
                    if (communityRoles.Any())
                    {
                        var group = new InheritedTeamGroup
                            { SourceScopeType = "Community", SourceScopeName = parents.CommunityName };
                        group.Members.AddRange(MapTeamItems(communityRoles));
                        response.InheritedGroups.Add(group);
                    }

                    var hubRoles = await hubTask;
                    if (hubRoles.Any())
                    {
                        var group = new InheritedTeamGroup
                            { SourceScopeType = "Hub", SourceScopeName = parents.HubName };
                        group.Members.AddRange(MapTeamItems(hubRoles));
                        response.InheritedGroups.Add(group);
                    }
                }

                break;
            }
        }

        return response;
    }

    public override async Task<CommunitySettingsResponse> GetCommunitySettings(
        GetCommunitySettingsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, "Community", request.CommunityPublicId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var settings = await communityManagementService.GetSettingsAsync(request.CommunityPublicId);
        if (settings is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

        var response = new CommunitySettingsResponse
        {
            Name = settings.Name,
            Slug = settings.Slug,
            HideAdultDiscussionsFromLists = settings.HideAdultDiscussionsFromLists,
            Require2Fa = settings.Require2FA
        };
        if (settings.Description is not null) response.Description = settings.Description;
        if (settings.Timezone is not null) response.Timezone = settings.Timezone;
        if (settings.LanguageCode is not null) response.LanguageCode = settings.LanguageCode;
        response.AllowedDiscussionTypes.AddRange(settings.AllowedDiscussionTypes.Select(t => (int)t));
        return response;
    }

    public override async Task<UpdateCommunitySettingsResponse> UpdateCommunitySettings(
        UpdateCommunitySettingsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, "Community", request.CommunityPublicId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var updateRequest = new Snakk.Application.DTOs.Management.UpdateCommunitySettingsRequest
        {
            Name = request.Name,
            Description = request.HasDescription ? request.Description : null,
            Timezone = request.HasTimezone ? request.Timezone : null,
            LanguageCode = request.HasLanguageCode ? request.LanguageCode : null,
            AllowedDiscussionTypes = request.AllowedDiscussionTypes
                .Select(t => (Snakk.Shared.Enums.DiscussionTypeEnum)t).ToList(),
            HideAdultDiscussionsFromLists = request.HideAdultDiscussionsFromLists,
            Require2FA = request.Require2Fa
        };

        var result = await communityManagementService.UpdateSettingsAsync(
            request.CommunityPublicId, updateRequest);

        if (result is not null)
            await cache.RemoveAsync($"community-meta:{request.CommunityPublicId}");

        return result is null
            ? new UpdateCommunitySettingsResponse { Success = false, ErrorMessage = "Community not found" }
            : new UpdateCommunitySettingsResponse { Success = true };
    }

    public override async Task<HubSettingsResponse> GetHubSettings(
        GetHubSettingsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, "Hub", request.HubPublicId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var settings = await hubManagementService.GetSettingsAsync(request.HubPublicId);
        if (settings is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Hub not found"));

        var parentTypes = await allowedTypesService.GetParentAllowedTypesForHubAsync(request.HubPublicId);

        var response = new HubSettingsResponse
        {
            Slug = settings.Slug,
            Name = settings.Name,
            Require2Fa = settings.Require2FA
        };
        if (settings.Description is not null) response.Description = settings.Description;
        if (settings.LanguageCode is not null) response.LanguageCode = settings.LanguageCode;
        if (settings.CommunityLanguageCode is not null) response.CommunityLanguageCode = settings.CommunityLanguageCode;
        response.AllowedDiscussionTypes.AddRange(settings.AllowedDiscussionTypes.Select(t => (int)t));
        response.ModeratorUserIds.AddRange(settings.ModeratorUserIds);
        response.ParentAllowedTypes.AddRange(parentTypes.Select(t => (int)t));
        return response;
    }

    public override async Task<UpdateHubSettingsResponse> UpdateHubSettings(
        UpdateHubSettingsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, "Hub", request.HubPublicId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var updateRequest = new Snakk.Application.DTOs.Management.UpdateHubSettingsRequest
        {
            Name = request.Name,
            Description = request.HasDescription ? request.Description : null,
            LanguageCode = request.HasLanguageCode ? request.LanguageCode : null,
            AllowedDiscussionTypes = request.AllowedDiscussionTypes
                .Select(t => (Snakk.Shared.Enums.DiscussionTypeEnum)t).ToList(),
            Require2FA = request.Require2Fa
        };

        var result = await hubManagementService.UpdateSettingsAsync(
            request.HubPublicId, updateRequest);

        if (result is not null)
            await cache.RemoveAsync($"hub-meta:{request.HubPublicId}");

        return result is null
            ? new UpdateHubSettingsResponse { Success = false, ErrorMessage = "Hub not found" }
            : new UpdateHubSettingsResponse { Success = true };
    }

    public override async Task<SiteSettingsGrpcResponse> GetSiteSettings(
        GetSiteSettingsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        // Global admin only
        var isGlobalAdmin = await scopeData.IsGlobalAdminAsync(userId);
        if (!isGlobalAdmin)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Global admin access required"));

        var siteInfo = await settingsService.GetSiteInfoAsync();
        var response = new SiteSettingsGrpcResponse
        {
            SiteName = siteInfo.SiteName,
            Timezone = siteInfo.Timezone
        };
        if (!string.IsNullOrEmpty(siteInfo.SiteDescription)) response.SiteDescription = siteInfo.SiteDescription;
        return response;
    }

    public override async Task<UpdateSiteSettingsResponse> UpdateSiteSettings(
        UpdateSiteSettingsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var isGlobalAdmin = await scopeData.IsGlobalAdminAsync(userId);
        if (!isGlobalAdmin)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Global admin access required"));

        var dto = new Snakk.Application.DTOs.Settings.SiteInfoDto
        {
            SiteName = request.SiteName,
            SiteDescription = request.HasSiteDescription ? request.SiteDescription : string.Empty,
            Timezone = request.Timezone,
            LogoUrl = string.Empty,
            Language = "en"
        };

        await settingsService.UpdateSiteInfoAsync(dto, userId);
        return new UpdateSiteSettingsResponse { Success = true };
    }

    public override async Task<RegistrationSettingsGrpcResponse> GetRegistrationSettings(
        GetRegistrationSettingsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var isGlobalAdmin = await scopeData.IsGlobalAdminAsync(userId);
        if (!isGlobalAdmin)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Global admin access required"));

        var settings = await settingsService.GetRegistrationSettingsAsync();
        return new RegistrationSettingsGrpcResponse
        {
            Mode = settings.Mode,
            InviteCode = settings.InviteCode
        };
    }

    public override async Task<UpdateRegistrationSettingsResponse> UpdateRegistrationSettings(
        UpdateRegistrationSettingsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var isGlobalAdmin = await scopeData.IsGlobalAdminAsync(userId);
        if (!isGlobalAdmin)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Global admin access required"));

        var validModes = new[] { "Open", "InviteOnly", "Closed" };
        if (!validModes.Contains(request.Mode))
            return new UpdateRegistrationSettingsResponse { Success = false, ErrorMessage = "Invalid registration mode." };

        await settingsService.UpdateRegistrationSettingsAsync(
            new Snakk.Application.DTOs.Settings.RegistrationSettingsDto
            {
                Mode = request.Mode,
                InviteCode = request.InviteCode
            }, userId);

        return new UpdateRegistrationSettingsResponse { Success = true };
    }

    private static IEnumerable<TeamMemberItem> MapTeamItems(IEnumerable<UserRoleDto> roles) =>
        roles.Select(r => new TeamMemberItem
        {
            RolePublicId = r.PublicId,
            UserDisplayName = r.UserDisplayName,
            RoleType = r.Role,
            AssignedAtTicks = r.AssignedAt.Ticks
        });

    public override async Task<AddTeamMemberResponse> AddTeamMember(
        AddTeamMemberRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ManageTeam);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var targetUser = await scopeData.FindUserPublicIdByDisplayNameAsync(request.TargetUsername);

        if (targetUser is null)
            return new AddTeamMemberResponse { Success = false, ErrorMessage = "User not found" };

        var roleType = request.RoleType switch
        {
            "CommunityAdmin" => UserRoleType.CommunityAdmin,
            "CommunityMod" => UserRoleType.CommunityMod,
            "HubMod" => UserRoleType.HubMod,
            "SpaceMod" => UserRoleType.SpaceMod,
            _ => throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unknown role type: {request.RoleType}"))
        };

        string? communityPublicId = null, hubPublicId = null, spacePublicId = null;
        switch (request.ScopeType)
        {
            case "Community": communityPublicId = request.ScopePublicId; break;
            case "Hub": hubPublicId = request.ScopePublicId; break;
            case "Space": spacePublicId = request.ScopePublicId; break;
        }

        await moderationRepository.AssignRoleAsync(
            targetUser, roleType, communityPublicId, hubPublicId, spacePublicId, userId);

        return new AddTeamMemberResponse { Success = true };
    }

    public override async Task<RemoveTeamMemberResponse> RemoveTeamMember(
        RemoveTeamMemberRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        // Look up role to check scope permission
        var role = await moderationRepository.GetRoleByPublicIdAsync(request.RolePublicId);
        if (role is null)
            return new RemoveTeamMemberResponse { Success = false, ErrorMessage = "Role not found" };

        var scopeType = role.SpacePublicId is not null ? "Space"
            : role.HubPublicId is not null ? "Hub"
            : role.CommunityPublicId is not null ? "Community" : "Community";
        var scopeId = role.SpacePublicId ?? role.HubPublicId ?? role.CommunityPublicId ?? "";

        if (!string.IsNullOrEmpty(scopeId))
        {
            var hasPermission = await permissionService.HasPermissionAsync(
                userId, scopeType, scopeId, ManagePermissionEnum.ManageTeam);
            if (!hasPermission)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
        }

        await moderationRepository.RevokeRoleAsync(request.RolePublicId, userId);

        return new RemoveTeamMemberResponse { Success = true };
    }

    // ==================== Reports ====================

    public override async Task<GetReportsResponse> GetReports(
        GetReportsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ManageReports);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        int? statusId = request.HasStatusFilter ? request.StatusFilter : null;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;

        var result = await moderationRepository.GetReportsForScopeAsync(
            request.ScopeType, request.ScopePublicId, statusId, request.Offset, pageSize);

        var response = new GetReportsResponse { HasMore = result.HasMoreItems };
        response.Reports.AddRange(result.Items.Select(r =>
        {
            var targetType = r.ReportedPostPublicId is not null ? "Post"
                : r.ReportedDiscussionPublicId is not null ? "Discussion"
                : r.ReportedUserPublicId is not null ? "User" : "Unknown";

            var targetTitle = r.ReportedPostContentSnippet
                ?? r.ReportedDiscussionTitle
                ?? r.ReportedUserDisplayName
                ?? "";

            var item = new ReportItem
            {
                PublicId = r.PublicId,
                Status = r.Status,
                TargetType = targetType,
                TargetTitle = targetTitle,
                ReasonName = r.ReasonName ?? "",
                ReporterDisplayName = r.ReporterUserDisplayName,
                CreatedAtTicks = r.CreatedAt.Ticks
            };
            if (r.Details is not null) item.Details = r.Details;
            if (r.ResolvedByUserDisplayName is not null) item.ResolvedByDisplayName = r.ResolvedByUserDisplayName;
            if (r.ResolvedAt.HasValue) item.ResolvedAtTicks = r.ResolvedAt.Value.Ticks;
            return item;
        }));

        return response;
    }

    public override async Task<ResolveReportResponse> ResolveReport(
        ResolveReportRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        // Look up report to determine scope for permission check
        var report = await moderationRepository.GetReportDetailByPublicIdAsync(request.ReportPublicId);
        if (report is null)
            return new ResolveReportResponse { Success = false, ErrorMessage = "Report not found" };

        var scopeType = report.SpacePublicId is not null ? "Space"
            : report.HubPublicId is not null ? "Hub"
            : report.CommunityPublicId is not null ? "Community" : "Community";
        var scopeId = report.SpacePublicId ?? report.HubPublicId ?? report.CommunityPublicId ?? "";

        if (!string.IsNullOrEmpty(scopeId))
        {
            var hasPermission = await permissionService.HasPermissionAsync(
                userId, scopeType, scopeId, ManagePermissionEnum.ManageReports);
            if (!hasPermission)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
        }

        string? resolutionNote = request.HasResolutionNote ? request.ResolutionNote : null;
        await moderationRepository.ResolveReportAsync(
            request.ReportPublicId, userId, resolutionNote, request.Dismiss);

        return new ResolveReportResponse { Success = true };
    }

    // ==================== Moderation Log ====================

    public override async Task<GetModerationLogResponse> GetModerationLog(
        GetModerationLogRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ViewDashboard);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var pageSize = request.PageSize > 0 ? request.PageSize : 50;
        var result = await moderationRepository.GetModerationLogForScopeAsync(
            request.ScopeType, request.ScopePublicId, request.Offset, pageSize);

        var response = new GetModerationLogResponse { HasMore = result.HasMoreItems };
        response.Entries.AddRange(result.Items
            .Where(e => !request.HasActionFilter || e.Action == request.ActionFilter)
            .Select(e => new ModerationLogItem
            {
                ActionType = e.Action,
                ActorDisplayName = e.ActorUserDisplayName,
                TargetName = e.TargetUserDisplayName ?? e.TargetDiscussionTitle ?? e.TargetPostPublicId ?? "",
                Reason = e.Reason ?? "",
                Details = e.Details ?? "",
                CreatedAtTicks = e.CreatedAt.Ticks
            }));

        return response;
    }

    // ==================== Content Moderation ====================

    public override async Task<GetFlaggedContentResponse> GetFlaggedContent(
        GetFlaggedContentRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ManageContent);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        // Get reports with pending status to find flagged content
        var pendingReports = await moderationRepository.GetReportsForScopeAsync(
            request.ScopeType, request.ScopePublicId, (int)ReportStatusEnum.Pending, 0, 200);

        // Group by target to get unique content items with report counts
        var postGroups = pendingReports.Items
            .Where(r => r.ReportedPostPublicId is not null)
            .GroupBy(r => r.ReportedPostPublicId!)
            .Select(g => new FlaggedContentItem
            {
                PublicId = g.Key,
                ContentType = "Post",
                AuthorDisplayName = "",
                Preview = g.First().ReportedPostContentSnippet ?? "",
                ReportCount = g.Count(),
                CreatedAtTicks = g.Min(r => r.CreatedAt).Ticks
            });

        var discussionGroups = pendingReports.Items
            .Where(r => r.ReportedDiscussionPublicId is not null && r.ReportedPostPublicId is null)
            .GroupBy(r => r.ReportedDiscussionPublicId!)
            .Select(g => new FlaggedContentItem
            {
                PublicId = g.Key,
                ContentType = "Discussion",
                AuthorDisplayName = "",
                Preview = g.First().ReportedDiscussionTitle ?? "",
                ReportCount = g.Count(),
                CreatedAtTicks = g.Min(r => r.CreatedAt).Ticks
            });

        var response = new GetFlaggedContentResponse();
        var items = postGroups.Concat(discussionGroups)
            .OrderByDescending(i => i.ReportCount)
            .Skip(request.Offset)
            .Take(request.PageSize > 0 ? request.PageSize : 50)
            .ToList();

        response.Items.AddRange(items);
        response.HasMore = postGroups.Count() + discussionGroups.Count() > request.Offset + items.Count;

        return response;
    }

    public override async Task<ModerateContentResponse> ModerateContent(
        ModerateContentRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        // Permission is checked inside the repository methods via CanModerateAsync
        string? reason = request.HasReason ? request.Reason : null;
        var ct = context.CancellationToken;

        switch (request.Action)
        {
            case "Delete" when request.ContentType == "Post":
                await moderationRepository.ModeratorDeletePostAsync(request.TargetPublicId, userId, reason, ct);
                break;
            case "Delete" when request.ContentType == "Discussion":
            {
                var spacePublicId = await scopeData.GetDiscussionSpacePublicIdAsync(request.TargetPublicId, ct);

                await moderationRepository.ModeratorDeleteDiscussionAsync(request.TargetPublicId, userId, reason, ct);

                if (spacePublicId is not null)
                {
                    await realtimeNotifier.NotifyDiscussionDeletedAsync(
                        DiscussionId.From(request.TargetPublicId),
                        SpaceId.From(spacePublicId));
                }
                break;
            }
            case "Lock":
                await moderationRepository.LockDiscussionAsync(request.TargetPublicId, userId, reason, ct);
                break;
            case "Unlock":
                await moderationRepository.UnlockDiscussionAsync(request.TargetPublicId, userId, ct);
                break;
            default:
                return new ModerateContentResponse { Success = false, ErrorMessage = $"Unknown action: {request.Action}" };
        }

        return new ModerateContentResponse { Success = true };
    }

    // ==================== Dashboard Stats ====================

    public override async Task<GetDashboardStatsResponse> GetDashboardStats(
        GetDashboardStatsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ViewDashboard);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var openReports = await moderationRepository.GetOpenReportCountForScopeAsync(
            request.ScopeType, request.ScopePublicId);
        var activeBans = await moderationRepository.GetActiveBanCountForScopeAsync(
            request.ScopeType, request.ScopePublicId);
        var teamMembers = (await moderationRepository.GetActiveRolesForScopeAsync(
            request.ScopeType, request.ScopePublicId)).Count();
        var recentLog = await moderationRepository.GetModerationLogForScopeAsync(
            request.ScopeType, request.ScopePublicId, 0, 10);

        var response = new GetDashboardStatsResponse
        {
            OpenReports = openReports,
            ActiveBans = activeBans,
            TeamMembers = teamMembers,
            RecentActions = recentLog.Items.Count()
        };

        response.RecentLog.AddRange(recentLog.Items.Select(e => new ModerationLogItem
        {
            ActionType = e.Action,
            ActorDisplayName = e.ActorUserDisplayName,
            TargetName = e.TargetUserDisplayName ?? e.TargetDiscussionTitle ?? e.TargetPostPublicId ?? "",
            Reason = e.Reason ?? "",
            Details = e.Details ?? "",
            CreatedAtTicks = e.CreatedAt.Ticks
        }));

        return response;
    }

    // ==================== Dashboard Charts ====================

    public override async Task<GetDashboardChartsResponse> GetDashboardCharts(
        GetDashboardChartsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ViewDashboard);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var days = request.Days > 0 ? request.Days : 30;
        var weeks = (days / 7) + 1;

        var activity = await dashboardChartRepository.GetDailyActivityAsync(
            request.ScopeType, request.ScopePublicId, days);
        var moderation = await dashboardChartRepository.GetWeeklyModerationAsync(
            request.ScopeType, request.ScopePublicId, weeks);

        var response = new GetDashboardChartsResponse();

        response.ActivitySeries.AddRange(activity.Select(a => new DailyActivityPoint
        {
            DateTicks = a.Date.Ticks,
            Discussions = a.Discussions,
            Posts = a.Posts
        }));

        response.ModerationSeries.AddRange(moderation.Select(m => new WeeklyModerationPoint
        {
            WeekStartTicks = m.WeekStart.Ticks,
            ReportsOpened = m.ReportsOpened,
            ReportsResolved = m.ReportsResolved
        }));

        return response;
    }

    // ==================== Dashboard Charts 2 ====================

    public override async Task<GetDashboardCharts2Response> GetDashboardCharts2(
        GetDashboardCharts2Request request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ViewDashboard);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var days = request.Days > 0 ? request.Days : 30;

        var reactionsTask = dashboardChartRepository.GetReactionBreakdownAsync(
            request.ScopeType, request.ScopePublicId, days);
        var contentTypesTask = dashboardChartRepository.GetContentTypeBreakdownAsync(
            request.ScopeType, request.ScopePublicId, days);
        var engagementTask = dashboardChartRepository.GetDailyEngagementAsync(
            request.ScopeType, request.ScopePublicId, days);
        var topDiscussionsTask = dashboardChartRepository.GetTopDiscussionsAsync(
            request.ScopeType, request.ScopePublicId, days);

        await Task.WhenAll(reactionsTask, contentTypesTask, engagementTask, topDiscussionsTask);

        var response = new GetDashboardCharts2Response();

        response.ReactionBreakdown.AddRange(reactionsTask.Result.Select(r => new ReactionBreakdownPoint
        {
            ReactionType = r.ReactionType,
            Count = r.Count
        }));

        response.ContentTypeBreakdown.AddRange(contentTypesTask.Result.Select(c => new ContentTypePoint
        {
            DiscussionType = c.DiscussionType,
            Count = c.Count
        }));

        response.EngagementSeries.AddRange(engagementTask.Result.Select(e => new DailyEngagementPoint
        {
            DateTicks = e.Date.Ticks,
            Reactions = e.Reactions
        }));

        response.TopDiscussions.AddRange(topDiscussionsTask.Result.Select(d => new TopDiscussionPoint
        {
            Title = d.Title,
            Slug = d.Slug,
            SpaceSlug = d.SpaceSlug,
            PostCount = d.PostCount,
            ReactionCount = d.ReactionCount
        }));

        return response;
    }

    // ==================== Statistics ====================

    public override async Task<GetScopeTopContributorsResponse> GetScopeTopContributors(
        GetScopeTopContributorsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ViewDashboard);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var days = request.Days > 0 ? request.Days : 30;
        var limit = request.Limit > 0 ? request.Limit : 10;

        var contributors = await dashboardChartRepository.GetTopContributorsForScopeAsync(
            request.ScopeType, request.ScopePublicId, days, limit);

        var response = new GetScopeTopContributorsResponse();
        response.Contributors.AddRange(contributors.Select(c => new ScopeContributorPoint
        {
            UserPublicId = c.UserPublicId,
            DisplayName = c.DisplayName,
            AvatarThumbnailFileName = c.AvatarThumbnailFileName ?? "",
            PostCount = c.PostCount,
            DiscussionCount = c.DiscussionCount
        }));
        return response;
    }

    public override async Task<GetPlatformUserStatsResponse> GetPlatformUserStats(
        GetPlatformUserStatsRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, "Site", "", ManagePermissionEnum.ViewDashboard);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var days = request.Days > 0 ? request.Days : 30;

        var registrationsTask = dashboardChartRepository.GetDailyRegistrationsAsync(days);
        var countsTask = dashboardChartRepository.GetUserActivityCountsAsync();

        await Task.WhenAll(registrationsTask, countsTask);

        var response = new GetPlatformUserStatsResponse
        {
            Dau = countsTask.Result.Dau,
            Wau = countsTask.Result.Wau,
            Mau = countsTask.Result.Mau,
            TotalUsers = countsTask.Result.Total
        };
        response.RegistrationSeries.AddRange(registrationsTask.Result.Select(r => new DailyRegistrationPoint
        {
            DateTicks = r.Date.Ticks,
            Count = r.Count
        }));
        return response;
    }

    // ==================== Banners ====================

    public override async Task<GetBannersResponse> GetBanners(
        GetBannersRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        if (!Enum.TryParse<BannerScopeEnum>(request.ScopeType, true, out var scope))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid scope type: {request.ScopeType}"));

        var announcements = await bannerUseCase.GetByScopeAsync(scope, request.ScopePublicId);

        var response = new GetBannersResponse();
        response.Banners.AddRange(announcements.Select(a =>
        {
            var item = new BannerItem
            {
                PublicId = a.PublicId.Value,
                Title = a.Title,
                Content = a.Content,
                RenderedContent = a.RenderedContent,
                Type = a.Type.ToString(),
                IsDismissible = a.IsDismissible,
                SortOrder = a.SortOrder,
                CreatedAtTicks = a.CreatedAt.Ticks
            };
            if (a.VisibleFrom.HasValue) item.VisibleFromTicks = a.VisibleFrom.Value.Ticks;
            if (a.VisibleUntil.HasValue) item.VisibleUntilTicks = a.VisibleUntil.Value.Ticks;
            if (a.LastModifiedAt.HasValue) item.LastModifiedAtTicks = a.LastModifiedAt.Value.Ticks;
            return item;
        }));

        return response;
    }

    public override async Task<CreateBannerResponse> CreateBanner(
        CreateBannerGrpcRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, request.ScopeType, request.ScopePublicId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        if (!Enum.TryParse<BannerScopeEnum>(request.ScopeType, true, out var scope))
            return new CreateBannerResponse { Success = false, ErrorMessage = $"Invalid scope type: {request.ScopeType}" };

        if (!Enum.TryParse<BannerTypeEnum>(request.Type, true, out var type))
            return new CreateBannerResponse { Success = false, ErrorMessage = $"Invalid type: {request.Type}" };

        DateTime? visibleFrom = request.HasVisibleFromTicks
            ? new DateTime(request.VisibleFromTicks, DateTimeKind.Utc) : null;
        DateTime? visibleUntil = request.HasVisibleUntilTicks
            ? new DateTime(request.VisibleUntilTicks, DateTimeKind.Utc) : null;

        var result = await bannerUseCase.CreateAsync(
            scope, request.ScopePublicId, UserId.From(userId),
            request.Title, request.Content, type,
            visibleFrom, visibleUntil, request.IsDismissible, request.SortOrder);

        if (!result.IsSuccess)
            return new CreateBannerResponse { Success = false, ErrorMessage = result.Error };

        await realtimeNotifier.NotifyBannerUpdatedAsync(request.ScopeType, request.ScopePublicId);
        await cache.RemoveAsync(BannerCacheKey(scope, request.ScopePublicId));

        return new CreateBannerResponse { Success = true, PublicId = result.Value!.PublicId.Value };
    }

    public override async Task<UpdateBannerResponse> UpdateBanner(
        UpdateBannerGrpcRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        // Look up banner to determine its scope for permission check
        var existing = await bannerUseCase.GetByIdAsync(BannerId.From(request.PublicId));
        if (!existing.IsSuccess)
            return new UpdateBannerResponse { Success = false, ErrorMessage = "Banner not found" };

        var a = existing.Value!;
        var hasPermission = await permissionService.HasPermissionAsync(
            userId, a.Scope.ToString(), a.ScopeEntityId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        if (!Enum.TryParse<BannerTypeEnum>(request.Type, true, out var type))
            return new UpdateBannerResponse { Success = false, ErrorMessage = $"Invalid type: {request.Type}" };

        DateTime? visibleFrom = request.HasVisibleFromTicks
            ? new DateTime(request.VisibleFromTicks, DateTimeKind.Utc) : null;
        DateTime? visibleUntil = request.HasVisibleUntilTicks
            ? new DateTime(request.VisibleUntilTicks, DateTimeKind.Utc) : null;

        var result = await bannerUseCase.UpdateAsync(
            BannerId.From(request.PublicId),
            request.Title, request.Content, type,
            visibleFrom, visibleUntil, request.IsDismissible, request.SortOrder);

        if (!result.IsSuccess)
            return new UpdateBannerResponse { Success = false, ErrorMessage = result.Error };

        await realtimeNotifier.NotifyBannerUpdatedAsync(a.Scope.ToString(), a.ScopeEntityId);
        await cache.RemoveAsync(BannerCacheKey(a.Scope, a.ScopeEntityId));

        return new UpdateBannerResponse { Success = true };
    }

    public override async Task<DeleteBannerResponse> DeleteBanner(
        DeleteBannerRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        // Look up banner to determine its scope for permission check
        var existing = await bannerUseCase.GetByIdAsync(BannerId.From(request.PublicId));
        if (!existing.IsSuccess)
            return new DeleteBannerResponse { Success = false, ErrorMessage = "Banner not found" };

        var a = existing.Value!;
        var hasPermission = await permissionService.HasPermissionAsync(
            userId, a.Scope.ToString(), a.ScopeEntityId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var result = await bannerUseCase.DeleteAsync(BannerId.From(request.PublicId));
        if (!result.IsSuccess)
            return new DeleteBannerResponse { Success = false, ErrorMessage = result.Error };

        await realtimeNotifier.NotifyBannerUpdatedAsync(a.Scope.ToString(), a.ScopeEntityId);
        await cache.RemoveAsync(BannerCacheKey(a.Scope, a.ScopeEntityId));

        return new DeleteBannerResponse { Success = true };
    }

    public override async Task<SendGlobalBannerResponse> SendGlobalBanner(
        SendGlobalBannerRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        await realtimeNotifier.NotifyGlobalAsync(request.EventType, request.Message);

        return new SendGlobalBannerResponse { Success = true };
    }

    public override async Task<CreateAnnouncementDiscussionResponse> CreateAnnouncementDiscussion(
        CreateAnnouncementDiscussionRequest request, ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        // Require at least SpaceMod permission
        var hasPermission = await permissionService.HasPermissionAsync(
            userId, "Space", request.SpacePublicId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Only moderators can create announcements"));

        var slug = SlugHelper.ToSlug(request.Title);
        var result = await discussionUseCase.CreateDiscussionAsync(
            Snakk.Domain.ValueObjects.SpaceId.From(request.SpacePublicId),
            Snakk.Domain.ValueObjects.UserId.From(userId),
            request.Title,
            slug,
            request.Content,
            Snakk.Shared.Enums.DiscussionTypeEnum.Announcement);

        if (!result.IsSuccess || result.Value is null)
            return new CreateAnnouncementDiscussionResponse
            {
                Success = false,
                ErrorMessage = result.Error ?? "Failed to create banner discussion"
            };

        return new CreateAnnouncementDiscussionResponse
        {
            Success = true,
            DiscussionPublicId = result.Value.PublicId.Value,
            DiscussionSlug = result.Value.Slug
        };
    }

    private static string? GetUserId(ServerCallContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.FindFirstValue("sub")
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static string BannerCacheKey(BannerScopeEnum scope, string entityId) =>
        scope switch
        {
            BannerScopeEnum.Community => $"banners:community:{entityId}",
            BannerScopeEnum.Hub      => $"banners:hub:{entityId}",
            BannerScopeEnum.Space    => $"banners:space:{entityId}",
            _                        => $"banners:community:{entityId}"
        };

    private static bool IsValidDiscordWebhookUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase))
        && uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.OrdinalIgnoreCase);

    private static ResolveScopeResponse BuildResponse(
        string scopeType, string scopePublicId, string scopeName,
        string communitySlug, string? hubSlug, string? spaceSlug,
        string communityName, string? hubName, string? spaceName,
        ManagePermissionSet permissions,
        string communityPublicId = "",
        string? scopeAvatarUrl = null)
    {
        var response = new ResolveScopeResponse
        {
            ScopeType = scopeType,
            ScopePublicId = scopePublicId,
            ScopeName = scopeName,
            CommunitySlug = communitySlug,
            CommunityName = communityName,
            CommunityPublicId = communityPublicId
        };

        if (scopeAvatarUrl is not null) response.ScopeAvatarUrl = scopeAvatarUrl;
        if (hubSlug is not null) response.HubSlug = hubSlug;
        if (spaceSlug is not null) response.SpaceSlug = spaceSlug;
        if (hubName is not null) response.HubName = hubName;
        if (spaceName is not null) response.SpaceName = spaceName;

        response.Permissions.AddRange(
            permissions.GetGrantedPermissions().Select(p => (int)p));

        return response;
    }

    // === Community Groups ===

    public override async Task<GetCommunityGroupsResponse> GetCommunityGroups(
        GetCommunityGroupsRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(userId, "Community", request.CommunityId, ManagePermissionEnum.ManageGroups);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var result = await groupService.GetGroupsAsync(request.CommunityId, context.CancellationToken);
        var response = new GetCommunityGroupsResponse();
        response.Groups.AddRange(result.Groups.Select(MapGroupItem));
        return response;
    }

    public override async Task<CommunityGroupItem> GetCommunityGroup(
        GetCommunityGroupRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(userId, "Community", request.CommunityId, ManagePermissionEnum.ManageGroups);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var group = await groupService.GetGroupAsync(request.CommunityId, request.GroupId, context.CancellationToken);
        if (group is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Group not found"));

        return MapGroupItem(group);
    }

    public override async Task<CommunityGroupItem> CreateCommunityGroup(
        CreateCommunityGroupRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(userId, "Community", request.CommunityId, ManagePermissionEnum.ManageGroups);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var appRequest = new Application.DTOs.Management.CreateGroupRequest
        {
            Name = request.Name,
            Slug = request.HasSlug ? request.Slug : null,
            Description = request.HasDescription ? request.Description : null,
            IsPublic = request.IsPublic,
            SortOrder = request.SortOrder
        };

        var group = await groupService.CreateGroupAsync(request.CommunityId, appRequest, userId, context.CancellationToken);
        return MapGroupItem(group);
    }

    public override async Task<CommunityGroupItem> UpdateCommunityGroup(
        UpdateCommunityGroupRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(userId, "Community", request.CommunityId, ManagePermissionEnum.ManageGroups);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var appRequest = new Application.DTOs.Management.UpdateGroupRequest
        {
            Name = request.Name,
            Description = request.HasDescription ? request.Description : null,
            IsPublic = request.IsPublic,
            SortOrder = request.SortOrder
        };

        var group = await groupService.UpdateGroupAsync(request.CommunityId, request.GroupId, appRequest, context.CancellationToken);
        if (group is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Group not found"));

        return MapGroupItem(group);
    }

    public override async Task<DeleteCommunityGroupResponse> DeleteCommunityGroup(
        DeleteCommunityGroupRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(userId, "Community", request.CommunityId, ManagePermissionEnum.ManageGroups);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var success = await groupService.DeleteGroupAsync(request.CommunityId, request.GroupId, context.CancellationToken);
        return new DeleteCommunityGroupResponse { Success = success };
    }

    public override async Task<GetCommunityGroupMembersResponse> GetCommunityGroupMembers(
        GetCommunityGroupMembersRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(userId, "Community", request.CommunityId, ManagePermissionEnum.ManageGroups);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var pageSize = request.PageSize > 0 ? Math.Min(request.PageSize, 100) : 50;
        var page = request.Page > 0 ? request.Page : 1;

        var result = await groupService.GetMembersAsync(request.CommunityId, request.GroupId, page, pageSize, context.CancellationToken);

        var response = new GetCommunityGroupMembersResponse
        {
            Total = result.Total,
            Page = result.Page,
            PageSize = result.PageSize
        };
        response.Members.AddRange(result.Members.Select(m => new CommunityGroupMemberItem
        {
            UserPublicId = m.UserPublicId,
            DisplayName = m.DisplayName,
            AddedAtTicks = m.AddedAt.Ticks
        }));
        return response;
    }

    public override async Task<AddCommunityGroupMemberResponse> AddCommunityGroupMember(
        AddCommunityGroupMemberRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(userId, "Community", request.CommunityId, ManagePermissionEnum.ManageGroups);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var appRequest = new Application.DTOs.Management.AddGroupMemberRequest
        {
            UserPublicId = request.UserPublicId
        };

        var success = await groupService.AddMemberAsync(request.CommunityId, request.GroupId, appRequest, userId, context.CancellationToken);
        return new AddCommunityGroupMemberResponse { Success = success };
    }

    public override async Task<RemoveCommunityGroupMemberResponse> RemoveCommunityGroupMember(
        RemoveCommunityGroupMemberRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var hasPermission = await permissionService.HasPermissionAsync(userId, "Community", request.CommunityId, ManagePermissionEnum.ManageGroups);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var success = await groupService.RemoveMemberAsync(request.CommunityId, request.GroupId, request.UserPublicId, context.CancellationToken);
        return new RemoveCommunityGroupMemberResponse { Success = success };
    }

    // === Group Access Control ===

    public override async Task<EntityAccessResponse> GetEntityAccess(
        GetEntityAccessRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        // Resolve community for permission check
        var scopeId = request.HasSpacePublicId ? request.SpacePublicId
            : request.HasHubPublicId ? request.HubPublicId
            : request.CommunityPublicId;
        var scopeType = request.HasSpacePublicId ? "Space"
            : request.HasHubPublicId ? "Hub"
            : "Community";

        var hasPermission = await permissionService.HasPermissionAsync(userId, scopeType, scopeId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var dto = await groupAccessService.GetEntityAccessAsync(
            request.HasCommunityPublicId ? request.CommunityPublicId : null,
            request.HasHubPublicId ? request.HubPublicId : null,
            request.HasSpacePublicId ? request.SpacePublicId : null,
            context.CancellationToken);

        var response = new EntityAccessResponse { IsRestricted = dto.IsRestricted };
        response.Grants.AddRange(dto.Grants.Select(g => new GroupAccessGrantItem
        {
            GroupPublicId = g.GroupPublicId,
            GroupName = g.GroupName,
            AccessLevel = (int)g.AccessLevel
        }));
        return response;
    }

    public override async Task<SetEntityRestrictedResponse> SetEntityRestricted(
        SetEntityRestrictedRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var scopeId = request.HasSpacePublicId ? request.SpacePublicId
            : request.HasHubPublicId ? request.HubPublicId
            : request.CommunityPublicId;
        var scopeType = request.HasSpacePublicId ? "Space"
            : request.HasHubPublicId ? "Hub"
            : "Community";

        var hasPermission = await permissionService.HasPermissionAsync(userId, scopeType, scopeId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        await groupAccessService.SetIsRestrictedAsync(
            request.IsRestricted,
            request.HasCommunityPublicId ? request.CommunityPublicId : null,
            request.HasHubPublicId ? request.HubPublicId : null,
            request.HasSpacePublicId ? request.SpacePublicId : null,
            context.CancellationToken);

        // Invalidate the meta cache for the affected entity — IsRestricted is cached in all three.
        if (request.HasSpacePublicId)
            await cache.RemoveAsync($"space-meta:{request.SpacePublicId}");
        else if (request.HasHubPublicId)
            await cache.RemoveAsync($"hub-meta:{request.HubPublicId}");
        else if (request.HasCommunityPublicId)
            await cache.RemoveAsync($"community-meta:{request.CommunityPublicId}");

        return new SetEntityRestrictedResponse { Success = true };
    }

    public override async Task<UpsertGroupGrantResponse> UpsertGroupGrant(
        UpsertGroupGrantRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var scopeId = request.HasSpacePublicId ? request.SpacePublicId
            : request.HasHubPublicId ? request.HubPublicId
            : request.CommunityPublicId;
        var scopeType = request.HasSpacePublicId ? "Space"
            : request.HasHubPublicId ? "Hub"
            : "Community";

        var hasPermission = await permissionService.HasPermissionAsync(userId, scopeType, scopeId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        await groupAccessService.UpsertGrantAsync(
            new Application.DTOs.Management.UpsertGroupGrantRequest
            {
                GroupPublicId = request.GroupPublicId,
                AccessLevel = (Snakk.Shared.Enums.AccessLevelEnum)request.AccessLevel
            },
            request.HasCommunityPublicId ? request.CommunityPublicId : null,
            request.HasHubPublicId ? request.HubPublicId : null,
            request.HasSpacePublicId ? request.SpacePublicId : null,
            context.CancellationToken);

        return new UpsertGroupGrantResponse { Success = true };
    }

    public override async Task<DeleteGroupGrantResponse> DeleteGroupGrant(
        DeleteGroupGrantRequest request,
        ServerCallContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var scopeId = request.HasSpacePublicId ? request.SpacePublicId
            : request.HasHubPublicId ? request.HubPublicId
            : request.CommunityPublicId;
        var scopeType = request.HasSpacePublicId ? "Space"
            : request.HasHubPublicId ? "Hub"
            : "Community";

        var hasPermission = await permissionService.HasPermissionAsync(userId, scopeType, scopeId, ManagePermissionEnum.ManageSettings);
        if (!hasPermission)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

        var success = await groupAccessService.DeleteGrantAsync(
            request.GroupPublicId,
            request.HasCommunityPublicId ? request.CommunityPublicId : null,
            request.HasHubPublicId ? request.HubPublicId : null,
            request.HasSpacePublicId ? request.SpacePublicId : null,
            context.CancellationToken);

        return new DeleteGroupGrantResponse { Success = success };
    }

    private static CommunityGroupItem MapGroupItem(Application.DTOs.Management.GroupDto g) =>
        new()
        {
            PublicId = g.PublicId,
            Name = g.Name,
            Slug = g.Slug,
            Description = g.Description ?? string.Empty,
            IsPublic = g.IsPublic,
            SortOrder = g.SortOrder,
            MemberCount = g.MemberCount,
            CreatedAtTicks = g.CreatedAt.Ticks
        };
}
