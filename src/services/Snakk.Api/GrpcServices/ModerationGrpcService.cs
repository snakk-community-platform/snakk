using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Snakk.Api.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Protos.Moderation;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class ModerationGrpcService(
    ModerationUseCase moderationUseCase,
    ICurrentUserService currentUser,
    SnakkDbContext dbContext) : ModerationService.ModerationServiceBase
{
    // ==================== Permission Checks ====================

    public override async Task<CanModerateResponse> CanModerate(CanModerateRequest request, ServerCallContext context)
    {
        var userId = RequireAuthString();
        var canMod = await moderationUseCase.CanModerateAsync(
            userId,
            request.HasCommunityId ? request.CommunityId : null,
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null);

        return new CanModerateResponse { CanModerate = canMod };
    }

    public override async Task<CanAdministerResponse> CanAdminister(CanAdministerRequest request, ServerCallContext context)
    {
        var userId = RequireAuthString();
        var canAdmin = await moderationUseCase.CanAdministerAsync(
            userId,
            request.HasCommunityId ? request.CommunityId : null,
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null);

        return new CanAdministerResponse { CanAdminister = canAdmin };
    }

    // ==================== Role Management ====================

    public override async Task<RoleListResponse> GetMyRoles(GetMyRolesRequest request, ServerCallContext context)
    {
        var userId = RequireAuthString();

        return await GetUserRolesInternal(userId);
    }

    public override async Task<RoleListResponse> GetUserRoles(GetUserRolesRequest request, ServerCallContext context) =>
        await GetUserRolesInternal(request.UserId);

    public override async Task<RoleListResponse> GetRolesForCommunity(GetRolesForScopeRequest request, ServerCallContext context)
    {
        // Get roles filtered by community scope - delegate to use case
        var roles = await moderationUseCase.GetUserRolesAsync(request.ScopeId);

        return MapRolesToResponse(roles);
    }

    public override async Task<RoleListResponse> GetRolesForHub(GetRolesForScopeRequest request, ServerCallContext context)
    {
        var roles = await moderationUseCase.GetUserRolesAsync(request.ScopeId);

        return MapRolesToResponse(roles);
    }

    public override async Task<RoleListResponse> GetRolesForSpace(GetRolesForScopeRequest request, ServerCallContext context)
    {
        var roles = await moderationUseCase.GetUserRolesAsync(request.ScopeId);

        return MapRolesToResponse(roles);
    }

    public override async Task<RoleInfo> AssignRole(AssignRoleRequest request, ServerCallContext context)
    {
        var assignerUserId = RequireAuthString();

        if (!System.Enum.TryParse<UserRoleTypeEnum>(request.Role, true, out var roleTypeEnum))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid role type"));

        var roleType = roleTypeEnum.ToDomain();

        var result = await moderationUseCase.AssignRoleAsync(
            request.UserId,
            roleType,
            request.HasCommunityId ? request.CommunityId : null,
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            assignerUserId);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to assign role"));

        return MapRoleToInfo(result.Value);
    }

    public override async Task<RevokeRoleResponse> RevokeRole(RevokeRoleRequest request, ServerCallContext context)
    {
        var revokerUserId = RequireAuthString();
        var result = await moderationUseCase.RevokeRoleAsync(request.RoleId, revokerUserId);

        if (!result.IsSuccess)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to revoke role"));

        return new RevokeRoleResponse { Success = true };
    }

    // ==================== Ban Management ====================

    public override async Task<BanListResponse> GetUserBans(GetUserBansRequest request, ServerCallContext context)
    {
        // ModerationUseCase doesn't have a GetUserBans method directly
        // Check if user is banned as a simpler check
        var isBanned = await moderationUseCase.IsUserBannedAsync(request.UserId);
        var response = new BanListResponse();

        // Return empty list for now - full ban history would require a new use case method
        return response;
    }

    public override async Task<BanStatusResponse> CheckUserBan(CheckUserBanRequest request, ServerCallContext context)
    {
        // TODO: ModerationUseCase.IsUserBannedAsync currently only supports spacePublicId scope.
        // community_id and hub_id from the request are available but not yet forwarded.
        var isBanned = await moderationUseCase.IsUserBannedAsync(
            request.UserId,
            request.HasSpaceId ? request.SpaceId : null);

        return new BanStatusResponse { IsBanned = isBanned };
    }

    public override async Task<BanInfo> BanUser(BanUserRequest request, ServerCallContext context)
    {
        var bannerUserId = RequireAuthString();

        if (!System.Enum.TryParse<BanTypeEnum>(request.BanType, true, out var banTypeEnum))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ban type"));

        var banType = banTypeEnum.ToDomain();

        DateTime? expiresAt = request.ExpiresAt is not null ? request.ExpiresAt.ToDateTime() : null;

        var result = await moderationUseCase.BanUserAsync(
            request.UserId,
            banType,
            request.HasCommunityId ? request.CommunityId : null,
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasReason ? request.Reason : null,
            expiresAt,
            bannerUserId);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to ban user"));

        return MapBanToInfo(result.Value);
    }

    public override async Task<UnbanResponse> UnbanUser(UnbanUserRequest request, ServerCallContext context)
    {
        var unbannerUserId = RequireAuthString();
        var result = await moderationUseCase.UnbanUserAsync(request.BanId, unbannerUserId);

        if (!result.IsSuccess)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to unban user"));

        return new UnbanResponse { Success = true };
    }

    // ==================== Report Management ====================

    public override async Task<PendingReportCountResponse> GetPendingReportCount(GetPendingReportCountRequest request, ServerCallContext context)
    {
        var userId = RequireAuthString();
        var count = await moderationUseCase.GetPendingReportCountAsync(userId);

        return new PendingReportCountResponse { Count = count };
    }

    public override async Task<PagedReportList> GetReports(GetReportsRequest request, ServerCallContext context)
    {
        var userId = RequireAuthString();
        var statusId = request.HasStatusId ? (int?)request.StatusId : null;

        var result = await moderationUseCase.GetReportsForModeratorAsync(
            userId, statusId, request.Offset, request.PageSize);

        var response = new PagedReportList
        {
            Total = result.Items.Count(),
            Offset = result.Offset,
            PageSize = result.PageSize
        };

        foreach (var r in result.Items)
        {
            var item = new ReportListItem
            {
                PublicId = r.PublicId,
                Status = r.Status,
                ReporterUserPublicId = r.ReporterUserPublicId,
                ReporterUserDisplayName = r.ReporterUserDisplayName,
                CreatedAt = ToTimestamp(r.CreatedAt),
                CommentCount = r.CommentCount
            };

            if (r.ReportedPostPublicId is not null) item.ReportedPostPublicId = r.ReportedPostPublicId;
            if (r.ReportedPostContentSnippet is not null) item.ReportedPostContentSnippet = r.ReportedPostContentSnippet;
            if (r.ReportedDiscussionPublicId is not null) item.ReportedDiscussionPublicId = r.ReportedDiscussionPublicId;
            if (r.ReportedDiscussionTitle is not null) item.ReportedDiscussionTitle = r.ReportedDiscussionTitle;
            if (r.ReportedUserPublicId is not null) item.ReportedUserPublicId = r.ReportedUserPublicId;
            if (r.ReportedUserDisplayName is not null) item.ReportedUserDisplayName = r.ReportedUserDisplayName;
            if (r.ReasonName is not null) item.ReasonName = r.ReasonName;
            if (r.Details is not null) item.Details = r.Details;
            if (r.ResolvedAt.HasValue) item.ResolvedAt = ToTimestamp(r.ResolvedAt.Value);
            if (r.ResolvedByUserPublicId is not null) item.ResolvedByUserPublicId = r.ResolvedByUserPublicId;
            if (r.ResolvedByUserDisplayName is not null) item.ResolvedByUserDisplayName = r.ResolvedByUserDisplayName;
            if (r.ResolutionNote is not null) item.ResolutionNote = r.ResolutionNote;
            if (r.SpacePublicId is not null) item.SpacePublicId = r.SpacePublicId;
            if (r.SpaceName is not null) item.SpaceName = r.SpaceName;
            if (r.HubPublicId is not null) item.HubPublicId = r.HubPublicId;
            if (r.HubName is not null) item.HubName = r.HubName;
            if (r.CommunityPublicId is not null) item.CommunityPublicId = r.CommunityPublicId;
            if (r.CommunityName is not null) item.CommunityName = r.CommunityName;

            response.Items.Add(item);
        }

        return response;
    }

    public override async Task<ReportDetailInfo> GetReportDetail(GetReportDetailRequest request, ServerCallContext context)
    {
        var detail = await moderationUseCase.GetReportDetailAsync(request.ReportId);

        if (detail is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Report not found"));

        var response = new ReportDetailInfo
        {
            PublicId = detail.PublicId,
            Status = detail.Status,
            ReporterUserPublicId = detail.ReporterUserPublicId,
            ReporterUserDisplayName = detail.ReporterUserDisplayName,
            CreatedAt = ToTimestamp(detail.CreatedAt)
        };

        if (detail.ReportedPostPublicId is not null) response.ReportedPostPublicId = detail.ReportedPostPublicId;
        if (detail.ReportedPostContent is not null) response.ReportedPostContent = detail.ReportedPostContent;
        if (detail.ReportedDiscussionPublicId is not null) response.ReportedDiscussionPublicId = detail.ReportedDiscussionPublicId;
        if (detail.ReportedDiscussionTitle is not null) response.ReportedDiscussionTitle = detail.ReportedDiscussionTitle;
        if (detail.ReportedUserPublicId is not null) response.ReportedUserPublicId = detail.ReportedUserPublicId;
        if (detail.ReportedUserDisplayName is not null) response.ReportedUserDisplayName = detail.ReportedUserDisplayName;
        if (detail.ReasonName is not null) response.ReasonName = detail.ReasonName;
        if (detail.ReasonDescription is not null) response.ReasonDescription = detail.ReasonDescription;
        if (detail.Details is not null) response.Details = detail.Details;
        if (detail.ResolvedAt.HasValue) response.ResolvedAt = ToTimestamp(detail.ResolvedAt.Value);
        if (detail.ResolvedByUserPublicId is not null) response.ResolvedByUserPublicId = detail.ResolvedByUserPublicId;
        if (detail.ResolvedByUserDisplayName is not null) response.ResolvedByUserDisplayName = detail.ResolvedByUserDisplayName;
        if (detail.ResolutionNote is not null) response.ResolutionNote = detail.ResolutionNote;
        if (detail.SpacePublicId is not null) response.SpacePublicId = detail.SpacePublicId;
        if (detail.SpaceName is not null) response.SpaceName = detail.SpaceName;
        if (detail.HubPublicId is not null) response.HubPublicId = detail.HubPublicId;
        if (detail.HubName is not null) response.HubName = detail.HubName;
        if (detail.CommunityPublicId is not null) response.CommunityPublicId = detail.CommunityPublicId;
        if (detail.CommunityName is not null) response.CommunityName = detail.CommunityName;

        if (detail.Comments is not null)
        {
            foreach (var c in detail.Comments)
            {
                var comment = new ReportCommentInfo
                {
                    PublicId = c.PublicId,
                    AuthorUserPublicId = c.AuthorUserPublicId,
                    AuthorUserDisplayName = c.AuthorUserDisplayName,
                    Content = c.Content,
                    CreatedAt = ToTimestamp(c.CreatedAt)
                };

                if (c.EditedAt.HasValue) comment.EditedAt = ToTimestamp(c.EditedAt.Value);

                response.Comments.Add(comment);
            }
        }

        return response;
    }

    public override async Task<ReportCreatedResponse> CreateReport(CreateReportRequest request, ServerCallContext context)
    {
        var userId = RequireAuthString();

        var result = await moderationUseCase.CreateReportAsync(
            userId,
            request.HasPostId ? request.PostId : null,
            request.HasDiscussionId ? request.DiscussionId : null,
            request.HasUserId ? request.UserId : null,
            request.HasReasonId ? request.ReasonId : null,
            request.HasDetails ? request.Details : null);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to create report"));

        return new ReportCreatedResponse
        {
            PublicId = result.Value.PublicId,
            Status = result.Value.Status,
            CreatedAt = ToTimestamp(result.Value.CreatedAt)
        };
    }

    public override async Task<ResolveReportResponse> ResolveReport(ResolveReportRequest request, ServerCallContext context)
    {
        var userId = RequireAuthString();

        var result = await moderationUseCase.ResolveReportAsync(
            request.ReportId,
            userId,
            request.HasResolutionNote ? request.ResolutionNote : null,
            request.Dismiss);

        if (!result.IsSuccess)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to resolve report"));

        return new ResolveReportResponse { Success = true };
    }

    public override async Task<ReportCommentInfo> AddReportComment(AddReportCommentRequest request, ServerCallContext context)
    {
        var userId = RequireAuthString();

        var result = await moderationUseCase.AddReportCommentAsync(
            request.ReportId, userId, request.Content);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to add comment"));

        var comment = new ReportCommentInfo
        {
            PublicId = result.Value.PublicId,
            AuthorUserPublicId = result.Value.AuthorUserPublicId,
            AuthorUserDisplayName = result.Value.AuthorUserDisplayName,
            Content = result.Value.Content,
            CreatedAt = ToTimestamp(result.Value.CreatedAt)
        };

        if (result.Value.EditedAt.HasValue) comment.EditedAt = ToTimestamp(result.Value.EditedAt.Value);

        return comment;
    }

    public override async Task<ReportReasonsResponse> GetReportReasons(GetReportReasonsRequest request, ServerCallContext context)
    {
        var reasons = await moderationUseCase.GetReportReasonsAsync(
            request.HasSpaceId ? request.SpaceId : null);

        var response = new ReportReasonsResponse();

        foreach (var r in reasons)
        {
            var item = new ReportReasonInfo
            {
                PublicId = r.PublicId,
                Name = r.Name,
                DisplayOrder = r.DisplayOrder
            };

            if (r.Description is not null) item.Description = r.Description;
            if (r.CommunityPublicId is not null) item.CommunityPublicId = r.CommunityPublicId;
            if (r.HubPublicId is not null) item.HubPublicId = r.HubPublicId;
            if (r.SpacePublicId is not null) item.SpacePublicId = r.SpacePublicId;

            response.Items.Add(item);
        }

        return response;
    }

    // ==================== Content Moderation ====================

    public override async Task<ContentModResponse> DeletePost(DeletePostRequest request, ServerCallContext context)
    {
        var userId = RequireAuthString();

        var result = await moderationUseCase.ModeratorDeletePostAsync(
            request.PostId, userId, request.HasReason ? request.Reason : null);

        if (!result.IsSuccess)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to delete post"));

        return new ContentModResponse { Success = true };
    }

    public override async Task<ContentModResponse> DeleteDiscussion(DeleteDiscussionRequest request, ServerCallContext context)
    {
        var userId = RequireAuthString();

        var result = await moderationUseCase.ModeratorDeleteDiscussionAsync(
            request.DiscussionId, userId, request.HasReason ? request.Reason : null);

        if (!result.IsSuccess)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to delete discussion"));

        return new ContentModResponse { Success = true };
    }

    public override async Task<ContentModResponse> LockDiscussion(LockDiscussionRequest request, ServerCallContext context)
    {
        var userId = RequireAuthString();

        var result = await moderationUseCase.LockDiscussionAsync(
            request.DiscussionId, userId, request.HasReason ? request.Reason : null);

        if (!result.IsSuccess)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to lock discussion"));

        return new ContentModResponse { Success = true };
    }

    public override async Task<ContentModResponse> UnlockDiscussion(UnlockDiscussionRequest request, ServerCallContext context)
    {
        var userId = RequireAuthString();

        var result = await moderationUseCase.UnlockDiscussionAsync(
            request.DiscussionId, userId);

        if (!result.IsSuccess)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to unlock discussion"));

        return new ContentModResponse { Success = true };
    }

    // ==================== Moderation Log ====================

    public override async Task<PagedModerationLogList> GetModerationLogs(GetModerationLogsRequest request, ServerCallContext context)
    {
        RequireAuthString();

        var result = await moderationUseCase.GetModerationLogAsync(
            request.HasCommunityId ? request.CommunityId : null,
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.Offset,
            request.PageSize);

        var response = new PagedModerationLogList
        {
            Total = result.Items.Count(),
            Offset = result.Offset,
            PageSize = result.PageSize
        };

        foreach (var log in result.Items)
        {
            var item = new ModerationLogItem
            {
                PublicId = log.PublicId,
                ActorUserPublicId = log.ActorUserPublicId,
                ActorUserDisplayName = log.ActorUserDisplayName,
                Action = log.Action,
                CreatedAt = ToTimestamp(log.CreatedAt)
            };

            if (log.TargetPostPublicId is not null) item.TargetPostPublicId = log.TargetPostPublicId;
            if (log.TargetDiscussionPublicId is not null) item.TargetDiscussionPublicId = log.TargetDiscussionPublicId;
            if (log.TargetDiscussionTitle is not null) item.TargetDiscussionTitle = log.TargetDiscussionTitle;
            if (log.TargetUserPublicId is not null) item.TargetUserPublicId = log.TargetUserPublicId;
            if (log.TargetUserDisplayName is not null) item.TargetUserDisplayName = log.TargetUserDisplayName;
            if (log.CommunityPublicId is not null) item.CommunityPublicId = log.CommunityPublicId;
            if (log.CommunityName is not null) item.CommunityName = log.CommunityName;
            if (log.HubPublicId is not null) item.HubPublicId = log.HubPublicId;
            if (log.HubName is not null) item.HubName = log.HubName;
            if (log.SpacePublicId is not null) item.SpacePublicId = log.SpacePublicId;
            if (log.SpaceName is not null) item.SpaceName = log.SpaceName;
            if (log.Details is not null) item.Details = log.Details;
            if (log.Reason is not null) item.Reason = log.Reason;

            response.Items.Add(item);
        }

        return response;
    }

    // ==================== Public Moderator List ====================

    public override async Task<GetModeratorsResponse> GetModerators(GetModeratorsRequest request, ServerCallContext context)
    {
        var response = new GetModeratorsResponse();
        var groups = new List<ModeratorGroup>();

        int? communityId = null;
        int? hubId = null;
        string? communityName = null;
        string? hubName = null;

        if (request.ScopeType == "Space")
        {
            var space = await dbContext.Spaces
                .Include(s => s.Hub).ThenInclude(h => h.Community)
                .Where(s => s.PublicId == request.ScopePublicId)
                .FirstOrDefaultAsync();

            if (space is null)
                throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

            hubId = space.HubId;
            hubName = space.Hub.Name;
            communityId = space.Hub.CommunityId;
            communityName = space.Hub.Community.Name;

            var spaceMods = await GetActiveRolesAsync(spaceId: space.Id);
            if (spaceMods.Count > 0)
                groups.Add(new ModeratorGroup { Level = "Space Moderators", ScopeName = space.Name, Moderators = { spaceMods } });
        }
        else if (request.ScopeType == "Hub")
        {
            var hub = await dbContext.Hubs
                .Include(h => h.Community)
                .Where(h => h.PublicId == request.ScopePublicId)
                .FirstOrDefaultAsync();

            if (hub is null)
                throw new RpcException(new Status(StatusCode.NotFound, "Hub not found"));

            hubId = hub.Id;
            hubName = hub.Name;
            communityId = hub.CommunityId;
            communityName = hub.Community.Name;
        }
        else if (request.ScopeType == "Community")
        {
            var community = await dbContext.Communities
                .Where(c => c.PublicId == request.ScopePublicId)
                .FirstOrDefaultAsync();

            if (community is null)
                throw new RpcException(new Status(StatusCode.NotFound, "Community not found"));

            communityId = community.Id;
            communityName = community.Name;
        }

        // Hub moderators (if scope is Space or Hub)
        if (hubId.HasValue)
        {
            var hubMods = await GetActiveRolesAsync(hubId: hubId.Value);
            if (hubMods.Count > 0)
                groups.Add(new ModeratorGroup { Level = "Hub Moderators", ScopeName = hubName ?? "", Moderators = { hubMods } });
        }

        // Community admins/mods
        if (communityId.HasValue)
        {
            var communityMods = await GetActiveRolesAsync(communityId: communityId.Value);
            if (communityMods.Count > 0)
                groups.Add(new ModeratorGroup { Level = "Community Team", ScopeName = communityName ?? "", Moderators = { communityMods } });
        }

        // Global admins
        var globalAdmins = await GetActiveRolesAsync(globalOnly: true);
        if (globalAdmins.Count > 0)
            groups.Add(new ModeratorGroup { Level = "Global Admins", ScopeName = "", Moderators = { globalAdmins } });

        response.Groups.AddRange(groups);
        response.TotalCount = groups.Sum(g => g.Moderators.Count);

        return response;
    }

    private async Task<List<ModeratorInfo>> GetActiveRolesAsync(
        int? communityId = null,
        int? hubId = null,
        int? spaceId = null,
        bool globalOnly = false)
    {
        var query = dbContext.UserRoles
            .Where(ur => ur.RevokedAt == null);

        if (globalOnly)
        {
            query = query.Where(ur => ur.RoleId == (int)UserRoleTypeEnum.GlobalAdmin);
        }
        else if (spaceId.HasValue)
        {
            query = query.Where(ur =>
                ur.SpaceId == spaceId.Value
                && ur.RoleId == (int)UserRoleTypeEnum.SpaceMod);
        }
        else if (hubId.HasValue)
        {
            query = query.Where(ur =>
                ur.HubId == hubId.Value
                && ur.RoleId == (int)UserRoleTypeEnum.HubMod);
        }
        else if (communityId.HasValue)
        {
            query = query.Where(ur =>
                ur.CommunityId == communityId.Value
                && (ur.RoleId == (int)UserRoleTypeEnum.CommunityAdmin
                    || ur.RoleId == (int)UserRoleTypeEnum.CommunityMod));
        }

        return await query
            .OrderBy(ur => ur.RoleId)
            .ThenBy(ur => ur.AssignedAt)
            .Select(ur => new ModeratorInfo
            {
                UserPublicId = ur.User.PublicId,
                DisplayName = ur.User.DisplayName,
                Role = ((UserRoleTypeEnum)ur.RoleId).ToString()
            })
            .ToListAsync();
    }

    // ==================== Helpers ====================

    private string RequireAuthString()
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userId = currentUser.GetCurrentUserId();

        if (userId is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        return userId;
    }

    private static Google.Protobuf.WellKnownTypes.Timestamp ToTimestamp(DateTime dt) =>
        Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));

    private static RoleInfo MapRoleToInfo(Snakk.Application.Repositories.UserRoleDto r)
    {
        var item = new RoleInfo
        {
            PublicId = r.PublicId,
            UserPublicId = r.UserPublicId,
            UserDisplayName = r.UserDisplayName,
            Role = r.Role,
            AssignedByUserPublicId = r.AssignedByUserPublicId,
            AssignedByUserDisplayName = r.AssignedByUserDisplayName,
            AssignedAt = ToTimestamp(r.AssignedAt)
        };

        if (r.CommunityPublicId is not null) item.CommunityId = r.CommunityPublicId;
        if (r.CommunityName is not null) item.CommunityName = r.CommunityName;
        if (r.HubPublicId is not null) item.HubId = r.HubPublicId;
        if (r.HubName is not null) item.HubName = r.HubName;
        if (r.SpacePublicId is not null) item.SpaceId = r.SpacePublicId;
        if (r.SpaceName is not null) item.SpaceName = r.SpaceName;
        if (r.RevokedAt.HasValue) item.RevokedAt = ToTimestamp(r.RevokedAt.Value);

        return item;
    }

    private static BanInfo MapBanToInfo(Snakk.Application.Repositories.UserBanDto ban)
    {
        var item = new BanInfo
        {
            PublicId = ban.PublicId,
            UserPublicId = ban.UserPublicId,
            UserDisplayName = ban.UserDisplayName,
            BanType = ban.BanType,
            BannedAt = ToTimestamp(ban.BannedAt),
            BannedByUserPublicId = ban.BannedByUserPublicId,
            BannedByUserDisplayName = ban.BannedByUserDisplayName
        };

        if (ban.CommunityPublicId is not null) item.CommunityId = ban.CommunityPublicId;
        if (ban.CommunityName is not null) item.CommunityName = ban.CommunityName;
        if (ban.HubPublicId is not null) item.HubId = ban.HubPublicId;
        if (ban.HubName is not null) item.HubName = ban.HubName;
        if (ban.SpacePublicId is not null) item.SpaceId = ban.SpacePublicId;
        if (ban.SpaceName is not null) item.SpaceName = ban.SpaceName;
        if (ban.Reason is not null) item.Reason = ban.Reason;
        if (ban.ExpiresAt.HasValue) item.ExpiresAt = ToTimestamp(ban.ExpiresAt.Value);
        if (ban.UnbannedAt.HasValue) item.UnbannedAt = ToTimestamp(ban.UnbannedAt.Value);
        if (ban.UnbannedByUserPublicId is not null) item.UnbannedByUserPublicId = ban.UnbannedByUserPublicId;
        if (ban.UnbannedByUserDisplayName is not null) item.UnbannedByUserDisplayName = ban.UnbannedByUserDisplayName;

        return item;
    }

    private async Task<RoleListResponse> GetUserRolesInternal(string userId)
    {
        var roles = await moderationUseCase.GetUserRolesAsync(userId);

        return MapRolesToResponse(roles);
    }

    private static RoleListResponse MapRolesToResponse(IEnumerable<Snakk.Application.Repositories.UserRoleDto> roles)
    {
        var response = new RoleListResponse();

        foreach (var r in roles)
        {
            response.Items.Add(MapRoleToInfo(r));
        }

        return response;
    }
}
