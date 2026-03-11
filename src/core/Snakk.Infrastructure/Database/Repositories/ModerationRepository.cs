namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Domain.Extensions;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class ModerationRepository(SnakkDbContext context) : IModerationRepository
{
    private readonly SnakkDbContext _context = context;

    // ==================== Role Management ====================

    public async Task<UserRoleDto?> GetRoleByPublicIdAsync(string publicId) => await _context.UserRoles
        .Where(ur => ur.PublicId == publicId)
        .Select(ur => new UserRoleDto(
            ur.PublicId,
            ur.User.PublicId,
            ur.User.DisplayName,
            ((UserRoleTypeEnum)ur.RoleId).ToString(),
            ur.Community != null ? ur.Community.PublicId : null,
            ur.Community != null ? ur.Community.Name : null,
            ur.Hub != null ? ur.Hub.PublicId : null,
            ur.Hub != null ? ur.Hub.Name : null,
            ur.Space != null ? ur.Space.PublicId : null,
            ur.Space != null ? ur.Space.Name : null,
            ur.AssignedByUser.PublicId,
            ur.AssignedByUser.DisplayName,
            ur.AssignedAt,
            ur.RevokedAt))
        .FirstOrDefaultAsync();

    public async Task<IEnumerable<UserRoleDto>> GetActiveRolesForUserAsync(string userPublicId) => await _context.UserRoles
        .Where(ur =>
            ur.User.PublicId == userPublicId
            && ur.RevokedAt == null)
        .Select(ur => new UserRoleDto(
            ur.PublicId,
            ur.User.PublicId,
            ur.User.DisplayName,
            ((UserRoleTypeEnum)ur.RoleId).ToString(),
            ur.Community != null ? ur.Community.PublicId : null,
            ur.Community != null ? ur.Community.Name : null,
            ur.Hub != null ? ur.Hub.PublicId : null,
            ur.Hub != null ? ur.Hub.Name : null,
            ur.Space != null ? ur.Space.PublicId : null,
            ur.Space != null ? ur.Space.Name : null,
            ur.AssignedByUser.PublicId,
            ur.AssignedByUser.DisplayName,
            ur.AssignedAt,
            ur.RevokedAt))
        .ToListAsync();

    public async Task<IEnumerable<UserRoleDto>> GetActiveRolesForCommunityAsync(string communityPublicId) => await _context.UserRoles
        .Where(ur =>
            ur.Community != null
            && ur.Community.PublicId == communityPublicId
            && ur.RevokedAt == null)
        .Select(ur => new UserRoleDto(
            ur.PublicId,
            ur.User.PublicId,
            ur.User.DisplayName,
            ((UserRoleTypeEnum)ur.RoleId).ToString(),
            ur.Community!.PublicId,
            ur.Community.Name,
            null, null, null, null,
            ur.AssignedByUser.PublicId,
            ur.AssignedByUser.DisplayName,
            ur.AssignedAt,
            ur.RevokedAt))
        .ToListAsync();

    public async Task<IEnumerable<UserRoleDto>> GetActiveRolesForHubAsync(string hubPublicId) => await _context.UserRoles
        .Where(ur =>
            ur.Hub != null
            && ur.Hub.PublicId == hubPublicId
            && ur.RevokedAt == null)
        .Select(ur => new UserRoleDto(
            ur.PublicId,
            ur.User.PublicId,
            ur.User.DisplayName,
            ((UserRoleTypeEnum)ur.RoleId).ToString(),
            null, null,
            ur.Hub!.PublicId,
            ur.Hub.Name,
            null, null,
            ur.AssignedByUser.PublicId,
            ur.AssignedByUser.DisplayName,
            ur.AssignedAt,
            ur.RevokedAt))
        .ToListAsync();

    public async Task<IEnumerable<UserRoleDto>> GetActiveRolesForSpaceAsync(string spacePublicId) => await _context.UserRoles
        .Where(ur =>
            ur.Space != null
            && ur.Space.PublicId == spacePublicId
            && ur.RevokedAt == null)
        .Select(ur => new UserRoleDto(
            ur.PublicId,
            ur.User.PublicId,
            ur.User.DisplayName,
            ((UserRoleTypeEnum)ur.RoleId).ToString(),
            null, null, null, null,
            ur.Space!.PublicId,
            ur.Space.Name,
            ur.AssignedByUser.PublicId,
            ur.AssignedByUser.DisplayName,
            ur.AssignedAt,
            ur.RevokedAt))
        .ToListAsync();

    public async Task<IEnumerable<UserRoleDto>> GetGlobalAdminsAsync() => await _context.UserRoles
        .Where(ur =>
            ur.RoleId == (int)UserRoleTypeEnum.GlobalAdmin
            && ur.RevokedAt == null)
        .Select(ur => new UserRoleDto(
            ur.PublicId,
            ur.User.PublicId,
            ur.User.DisplayName,
            ((UserRoleTypeEnum)ur.RoleId).ToString(),
            null, null, null, null, null, null,
            ur.AssignedByUser.PublicId,
            ur.AssignedByUser.DisplayName,
            ur.AssignedAt,
            ur.RevokedAt))
        .ToListAsync();

    public async Task<UserRoleDto> AssignRoleAsync(
        string targetUserPublicId,
        UserRoleType roleType,
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        string assignedByUserPublicId)
    {
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == targetUserPublicId)
            ?? throw new InvalidOperationException("Target user not found");

        var assigner = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == assignedByUserPublicId)
            ?? throw new InvalidOperationException("Assigning user not found");

        int? communityId = null, hubId = null, spaceId = null;
        string? communityName = null, hubName = null, spaceName = null;

        if (!string.IsNullOrEmpty(spacePublicId))
        {
            var space = await _context.Spaces.Include(s => s.Hub).FirstOrDefaultAsync(s => s.PublicId == spacePublicId)
                ?? throw new InvalidOperationException("Space not found");
            spaceId = space.Id;
            spaceName = space.Name;
        }
        else if (!string.IsNullOrEmpty(hubPublicId))
        {
            var hub = await _context.Hubs.FirstOrDefaultAsync(h => h.PublicId == hubPublicId)
                ?? throw new InvalidOperationException("Hub not found");
            hubId = hub.Id;
            hubName = hub.Name;
        }
        else if (!string.IsNullOrEmpty(communityPublicId))
        {
            var community = await _context.Communities.FirstOrDefaultAsync(c => c.PublicId == communityPublicId)
                ?? throw new InvalidOperationException("Community not found");
            communityId = community.Id;
            communityName = community.Name;
        }

        var role = new UserRoleDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = targetUser.Id,
            AssignedByUserId = assigner.Id,
            AssignedAt = DateTime.UtcNow,

            RoleId = (int)roleType.ToShared(),

            CommunityId = roleType == UserRoleType.CommunityAdmin || roleType == UserRoleType.CommunityMod ? communityId : null,
            HubId = roleType == UserRoleType.HubMod ? hubId : null,
            SpaceId = roleType == UserRoleType.SpaceMod ? spaceId : null,
        };

        _context.UserRoles.Add(role);
        await _context.SaveChangesAsync();

        // Log the action
        await LogModerationActionAsync(
            assignedByUserPublicId,
            "AssignRole",
            targetUserPublicId: targetUserPublicId,
            communityPublicId: communityPublicId,
            hubPublicId: hubPublicId,
            spacePublicId: spacePublicId,
            details: $"Assigned role {roleType}");

        return new UserRoleDto(
            role.PublicId,
            targetUserPublicId,
            targetUser.DisplayName,
            roleType.ToString(),
            communityPublicId, communityName,
            hubPublicId, hubName,
            spacePublicId, spaceName,
            assignedByUserPublicId,
            assigner.DisplayName,
            role.AssignedAt,
            null);
    }

    public async Task RevokeRoleAsync(
        string rolePublicId,
        string revokedByUserPublicId)
    {
        var role = await _context.UserRoles
            .AsTracking()
            .Include(ur => ur.User)
            .Include(ur => ur.Community)
            .Include(ur => ur.Hub)
            .Include(ur => ur.Space)
            .FirstOrDefaultAsync(ur => ur.PublicId == rolePublicId)
            ?? throw new InvalidOperationException("Role not found");

        var revoker = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == revokedByUserPublicId)
            ?? throw new InvalidOperationException("Revoking user not found");

        role.RevokedAt = DateTime.UtcNow;
        role.RevokedByUserId = revoker.Id;

        await _context.SaveChangesAsync();

        await LogModerationActionAsync(
            revokedByUserPublicId,
            "RevokeRole",
            targetUserPublicId: role.User.PublicId,
            communityPublicId: role.Community?.PublicId,
            hubPublicId: role.Hub?.PublicId,
            spacePublicId: role.Space?.PublicId,
            details: $"Revoked role {((UserRoleTypeEnum)role.RoleId).ToString()}");
    }

    public async Task<bool> CanModerateAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null)
    {
        var activeRoles = await _context.UserRoles
            .Where(ur =>
                ur.User.PublicId == userPublicId
                && ur.RevokedAt == null)
            .Select(ur => new {
                ur.RoleId,
                ur.HubId,
                CommunityPublicId = ur.Community != null ? ur.Community.PublicId : null,
                HubPublicId = ur.Hub != null ? ur.Hub.PublicId : null,
                SpacePublicId = ur.Space != null ? ur.Space.PublicId : null })
            .ToListAsync();

        foreach (var role in activeRoles)
        {
            if (role.RoleId == (int)UserRoleTypeEnum.GlobalAdmin)
                return true;

            if (!string.IsNullOrEmpty(communityPublicId) && role.CommunityPublicId == communityPublicId)
            {
                if (role.RoleId == (int)UserRoleTypeEnum.CommunityAdmin
                    || role.RoleId == (int)UserRoleTypeEnum.CommunityMod)
                    return true;
            }

            if (!string.IsNullOrEmpty(hubPublicId)
                && role.HubPublicId == hubPublicId
                && role.RoleId == (int)UserRoleTypeEnum.HubMod)
                return true;

            if (!string.IsNullOrEmpty(spacePublicId))
            {
                if (role.SpacePublicId == spacePublicId && role.RoleId == (int)UserRoleTypeEnum.SpaceMod)
                    return true;

                // Hub mod can moderate spaces in their hub
                if (role.RoleId == (int)UserRoleTypeEnum.HubMod && role.HubId.HasValue)
                {
                    if (await _context.Spaces.AnyAsync(s =>
                        s.PublicId == spacePublicId
                        && s.HubId == role.HubId))
                        return true;
                }
            }
        }

        return false;
    }

    public async Task<bool> CanAdministerAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null)
    {
        var activeRoles = await _context.UserRoles
            .Where(ur =>
                ur.User.PublicId == userPublicId
                && ur.RevokedAt == null)
            .Select(ur => new {
                ur.RoleId,
                ur.CommunityId,
                CommunityPublicId = ur.Community != null ? ur.Community.PublicId : null })
            .ToListAsync();

        foreach (var role in activeRoles)
        {
            if (role.RoleId == (int)UserRoleTypeEnum.GlobalAdmin)
                return true;

            if (role.RoleId == (int)UserRoleTypeEnum.CommunityAdmin && role.CommunityPublicId is not null)
            {
                if (!string.IsNullOrEmpty(communityPublicId) && role.CommunityPublicId == communityPublicId)
                    return true;

                if (!string.IsNullOrEmpty(hubPublicId))
                {
                    if (await _context.Hubs.AnyAsync(h =>
                        h.PublicId == hubPublicId
                        && h.CommunityId == role.CommunityId))
                        return true;
                }

                if (!string.IsNullOrEmpty(spacePublicId))
                {
                    if (await _context.Spaces.AnyAsync(s =>
                        s.PublicId == spacePublicId
                        && s.Hub.CommunityId == role.CommunityId))
                        return true;
                }
            }
        }

        return false;
    }

    // ==================== Ban Management ====================

    public async Task<UserBanDto?> GetBanByPublicIdAsync(string publicId) => await _context.UserBans
        .Where(ub => ub.PublicId == publicId)
        .Select(ub => new UserBanDto(
            ub.PublicId,
            ub.User.PublicId,
            ub.User.DisplayName,
            ((BanTypeEnum)ub.BanTypeId).ToString(),
            ub.Community != null ? ub.Community.PublicId : null,
            ub.Community != null ? ub.Community.Name : null,
            ub.Hub != null ? ub.Hub.PublicId : null,
            ub.Hub != null ? ub.Hub.Name : null,
            ub.Space != null ? ub.Space.PublicId : null,
            ub.Space != null ? ub.Space.Name : null,
            ub.Reason,
            ub.BannedAt,
            ub.ExpiresAt,
            ub.BannedByUser.PublicId,
            ub.BannedByUser.DisplayName,
            ub.UnbannedAt,
            ub.UnbannedByUser != null ? ub.UnbannedByUser.PublicId : null,
            ub.UnbannedByUser != null ? ub.UnbannedByUser.DisplayName : null))
        .FirstOrDefaultAsync();

    public async Task<IEnumerable<UserBanDto>> GetActiveBansForUserAsync(string userPublicId)
    {
        var now = DateTime.UtcNow;

        return await _context.UserBans
            .Where(ub =>
                ub.User.PublicId == userPublicId
                && ub.UnbannedAt == null
                && (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .Select(ub => new UserBanDto(
                ub.PublicId,
                ub.User.PublicId,
                ub.User.DisplayName,
                ((BanTypeEnum)ub.BanTypeId).ToString(),
                ub.Community != null ? ub.Community.PublicId : null,
                ub.Community != null ? ub.Community.Name : null,
                ub.Hub != null ? ub.Hub.PublicId : null,
                ub.Hub != null ? ub.Hub.Name : null,
                ub.Space != null ? ub.Space.PublicId : null,
                ub.Space != null ? ub.Space.Name : null,
                ub.Reason,
                ub.BannedAt,
                ub.ExpiresAt,
                ub.BannedByUser.PublicId,
                ub.BannedByUser.DisplayName,
                ub.UnbannedAt,
                null, null))
            .ToListAsync();
    }

    public async Task<UserBanDto?> GetActiveBanForScopeAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null)
    {
        var now = DateTime.UtcNow;
        var bans = await GetActiveBansForUserAsync(userPublicId);

        // Check platform-wide ban
        var platformBan = bans.FirstOrDefault(b =>
            b.CommunityPublicId == null
            && b.HubPublicId == null
            && b.SpacePublicId == null);

        if (platformBan is not null) return platformBan;

        // Check community ban
        if (!string.IsNullOrEmpty(communityPublicId))
        {
            var communityBan = bans.FirstOrDefault(b => b.CommunityPublicId == communityPublicId);

            if (communityBan is not null) return communityBan;
        }

        // Check hub ban
        if (!string.IsNullOrEmpty(hubPublicId))
        {
            var hubBan = bans.FirstOrDefault(b => b.HubPublicId == hubPublicId);

            if (hubBan is not null) return hubBan;
        }

        // Check space ban
        if (!string.IsNullOrEmpty(spacePublicId))
        {
            var spaceBan = bans.FirstOrDefault(b => b.SpacePublicId == spacePublicId);

            if (spaceBan is not null) return spaceBan;
        }

        return null;
    }

    public async Task<bool> IsUserBannedAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null)
    {
        var ban = await GetActiveBanForScopeAsync(userPublicId, communityPublicId, hubPublicId, spacePublicId);
        return ban is not null;
    }

    public async Task<UserBanDto> BanUserAsync(
        string targetUserPublicId,
        BanType banType,
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        string? reason,
        DateTime? expiresAt,
        string bannedByUserPublicId)
    {
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == targetUserPublicId)
            ?? throw new InvalidOperationException("Target user not found");

        var banner = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == bannedByUserPublicId)
            ?? throw new InvalidOperationException("Banning user not found");

        int? communityId = null, hubId = null, spaceId = null;
        string? communityName = null, hubName = null, spaceName = null;

        if (!string.IsNullOrEmpty(spacePublicId))
        {
            var space = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == spacePublicId)
                ?? throw new InvalidOperationException("Space not found");
            spaceId = space.Id;
            spaceName = space.Name;
        }
        else if (!string.IsNullOrEmpty(hubPublicId))
        {
            var hub = await _context.Hubs.FirstOrDefaultAsync(h => h.PublicId == hubPublicId)
                ?? throw new InvalidOperationException("Hub not found");
            hubId = hub.Id;
            hubName = hub.Name;
        }
        else if (!string.IsNullOrEmpty(communityPublicId))
        {
            var community = await _context.Communities.FirstOrDefaultAsync(c => c.PublicId == communityPublicId)
                ?? throw new InvalidOperationException("Community not found");
            communityId = community.Id;
            communityName = community.Name;
        }

        var ban = new UserBanDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = targetUser.Id,
            CommunityId = communityId,
            HubId = hubId,
            SpaceId = spaceId,
            Reason = reason,
            BannedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            BannedByUserId = banner.Id,

            BanTypeId = (int)banType.ToShared(),
        };

        _context.UserBans.Add(ban);
        await _context.SaveChangesAsync();

        await LogModerationActionAsync(
            bannedByUserPublicId,
            "BanUser",
            targetUserPublicId: targetUserPublicId,
            communityPublicId: communityPublicId,
            hubPublicId: hubPublicId,
            spacePublicId: spacePublicId,
            reason: reason,
            details: $"Banned user ({banType})" + (expiresAt.HasValue ? $" until {expiresAt}" : " permanently"));

        return new UserBanDto(
            ban.PublicId,
            targetUserPublicId,
            targetUser.DisplayName,
            banType.ToString(),
            communityPublicId, communityName,
            hubPublicId, hubName,
            spacePublicId, spaceName,
            reason,
            ban.BannedAt,
            expiresAt,
            bannedByUserPublicId,
            banner.DisplayName,
            null, null, null);
    }

    public async Task UnbanUserAsync(
        string banPublicId,
        string unbannedByUserPublicId)
    {
        var ban = await _context.UserBans
            .AsTracking()
            .Include(ub => ub.User)
            .Include(ub => ub.Community)
            .Include(ub => ub.Hub)
            .Include(ub => ub.Space)
            .FirstOrDefaultAsync(ub => ub.PublicId == banPublicId)
            ?? throw new InvalidOperationException("Ban not found");

        var unbanner = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == unbannedByUserPublicId)
            ?? throw new InvalidOperationException("Unbanning user not found");

        ban.UnbannedAt = DateTime.UtcNow;
        ban.UnbannedByUserId = unbanner.Id;

        await _context.SaveChangesAsync();

        await LogModerationActionAsync(
            unbannedByUserPublicId,
            "UnbanUser",
            targetUserPublicId: ban.User.PublicId,
            communityPublicId: ban.Community?.PublicId,
            hubPublicId: ban.Hub?.PublicId,
            spacePublicId: ban.Space?.PublicId,
            details: "Unbanned user");
    }

    // ==================== Report Management ====================

    public async Task<ReportDto?> GetReportByPublicIdAsync(string publicId) => await _context.Reports
        .Where(r => r.PublicId == publicId)
        .Select(r => new ReportDto(
            r.PublicId,
            ((ReportStatusEnum)r.StatusId).ToString(),
            r.ReporterUser.PublicId,
            r.ReportedPost != null ? r.ReportedPost.PublicId : null,
            r.ReportedDiscussion != null ? r.ReportedDiscussion.PublicId : null,
            r.ReportedUser != null ? r.ReportedUser.PublicId : null,
            r.Reason != null ? r.Reason.PublicId : null,
            r.Details,
            r.CreatedAt,
            r.ResolvedAt,
            r.ResolvedByUser != null ? r.ResolvedByUser.PublicId : null,
            r.ResolutionNote))
        .FirstOrDefaultAsync();

    public async Task<ReportDetailDto?> GetReportDetailByPublicIdAsync(string publicId) => await _context.Reports
        .Where(r => r.PublicId == publicId)
        .Select(r => new ReportDetailDto(
            r.PublicId,
            ((ReportStatusEnum)r.StatusId).ToString(),
            r.ReporterUser.PublicId,
            r.ReporterUser.DisplayName,
            r.ReportedPost != null ? r.ReportedPost.PublicId : null,
            r.ReportedPost != null ? r.ReportedPost.Content : null,
            r.ReportedDiscussion != null ? r.ReportedDiscussion.PublicId : null,
            r.ReportedDiscussion != null ? r.ReportedDiscussion.Title : null,
            r.ReportedUser != null ? r.ReportedUser.PublicId : null,
            r.ReportedUser != null ? r.ReportedUser.DisplayName : null,
            r.Reason != null ? r.Reason.Name : null,
            r.Reason != null ? r.Reason.Description : null,
            r.Details,
            r.CreatedAt,
            r.ResolvedAt,
            r.ResolvedByUser != null ? r.ResolvedByUser.PublicId : null,
            r.ResolvedByUser != null ? r.ResolvedByUser.DisplayName : null,
            r.ResolutionNote,
            r.Space != null ? r.Space.PublicId : null,
            r.Space != null ? r.Space.Name : null,
            r.Hub != null ? r.Hub.PublicId : null,
            r.Hub != null ? r.Hub.Name : null,
            r.Community != null ? r.Community.PublicId : null,
            r.Community != null ? r.Community.Name : null,
            r.Comments
                .Where(c => !c.IsDeleted)
                .Select(c => new ReportCommentDto(
                    c.PublicId,
                    c.AuthorUser.PublicId,
                    c.AuthorUser.DisplayName,
                    c.Content,
                    c.CreatedAt,
                    c.EditedAt))))
        .FirstOrDefaultAsync();

    public async Task<PagedResult<ReportListDto>> GetReportsForCommunityAsync(
        string communityPublicId,
        int? statusId,
        int offset,
        int pageSize)
    {
        var query = _context.Reports
            .Where(r => r.Community != null && r.Community.PublicId == communityPublicId);

        if (statusId.HasValue)
            query = query.Where(r => r.StatusId == statusId.Value);

        return await GetPagedReportsAsync(query, offset, pageSize);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForHubAsync(
        string hubPublicId,
        int? statusId,
        int offset,
        int pageSize)
    {
        var query = _context.Reports
            .Where(r =>
                (r.Hub != null && r.Hub.PublicId == hubPublicId)
                || (r.Space != null && r.Space.Hub.PublicId == hubPublicId));

        if (statusId.HasValue)
            query = query.Where(r => r.StatusId == statusId.Value);

        return await GetPagedReportsAsync(query, offset, pageSize);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForSpaceAsync(
        string spacePublicId,
        int? statusId,
        int offset,
        int pageSize)
    {
        var query = _context.Reports
            .Where(r => r.Space != null && r.Space.PublicId == spacePublicId);

        if (statusId.HasValue)
            query = query.Where(r => r.StatusId == statusId.Value);

        return await GetPagedReportsAsync(query, offset, pageSize);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForModeratorAsync(
        string moderatorPublicId,
        int? statusId,
        int offset,
        int pageSize)
    {
        // Get moderator's roles
        var roles = await GetActiveRolesForUserAsync(moderatorPublicId);

        if (!roles.Any())
            return new PagedResult<ReportListDto> { Items = [], Offset = offset, PageSize = pageSize, HasMoreItems = false };

        // Find highest level role (role.Role is now a string from DTO, so we still compare strings)
        var highestRole = roles.OrderBy(r => r.Role switch
        {
            "GlobalAdmin" => 0,
            "CommunityAdmin" => 1,
            "CommunityMod" => 2,
            "HubMod" => 3,
            "SpaceMod" => 4,
            _ => 5
        }).First();

        if (!string.IsNullOrEmpty(highestRole.CommunityPublicId))
            return await GetReportsForCommunityAsync(highestRole.CommunityPublicId, statusId, offset, pageSize);

        if (!string.IsNullOrEmpty(highestRole.HubPublicId))
            return await GetReportsForHubAsync(highestRole.HubPublicId, statusId, offset, pageSize);

        if (!string.IsNullOrEmpty(highestRole.SpacePublicId))
            return await GetReportsForSpaceAsync(highestRole.SpacePublicId, statusId, offset, pageSize);

        return new PagedResult<ReportListDto> { Items = [], Offset = offset, PageSize = pageSize, HasMoreItems = false };
    }

    public async Task<int> GetPendingReportCountForModeratorAsync(string moderatorPublicId)
    {
        var reports = await GetReportsForModeratorAsync(moderatorPublicId, (int)ReportStatusEnum.Pending, 0, 1000);
        return reports.Items.Count();
    }

    private async Task<PagedResult<ReportListDto>> GetPagedReportsAsync(
        IQueryable<ReportDatabaseEntity> query,
        int offset,
        int pageSize)
    {
        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(r => new ReportListDto(
                r.PublicId,
                ((ReportStatusEnum)r.StatusId).ToString(),
                r.ReporterUser.PublicId,
                r.ReporterUser.DisplayName,
                r.ReportedPost != null ? r.ReportedPost.PublicId : null,
                r.ReportedPost != null
                    ? r.ReportedPost.Content.Length > 100
                        ? r.ReportedPost.Content.Substring(0, 100) + "..."
                        : r.ReportedPost.Content
                    : null,
                r.ReportedDiscussion != null ? r.ReportedDiscussion.PublicId : null,
                r.ReportedDiscussion != null ? r.ReportedDiscussion.Title : null,
                r.ReportedUser != null ? r.ReportedUser.PublicId : null,
                r.ReportedUser != null ? r.ReportedUser.DisplayName : null,
                r.Reason != null ? r.Reason.Name : null,
                r.Details,
                r.CreatedAt,
                r.ResolvedAt,
                r.ResolvedByUser != null ? r.ResolvedByUser.PublicId : null,
                r.ResolvedByUser != null ? r.ResolvedByUser.DisplayName : null,
                r.ResolutionNote,
                r.Space != null ? r.Space.PublicId : null,
                r.Space != null ? r.Space.Name : null,
                r.Hub != null ? r.Hub.PublicId : null,
                r.Hub != null ? r.Hub.Name : null,
                r.Community != null ? r.Community.PublicId : null,
                r.Community != null ? r.Community.Name : null,
                r.Comments.Count(c => !c.IsDeleted)))
            .ToListAsync();

        return new PagedResult<ReportListDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = offset + items.Count < totalCount
        };
    }

    public async Task<ReportDto> CreateReportAsync(
        string reporterUserPublicId,
        string? reportedPostPublicId,
        string? reportedDiscussionPublicId,
        string? reportedUserPublicId,
        string? reasonPublicId,
        string? details)
    {
        var reporter = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == reporterUserPublicId)
            ?? throw new InvalidOperationException("Reporter not found");

        int? reportedPostId = null, reportedDiscussionId = null, reportedUserId = null;
        int? spaceId = null, hubId = null, communityId = null;

        if (!string.IsNullOrEmpty(reportedPostPublicId))
        {
            var post = await _context.Posts
                .Where(p => p.PublicId == reportedPostPublicId)
                .Select(p => new {
                    PostId = p.Id,
                    SpaceId = p.Discussion.SpaceId,
                    HubId = p.Discussion.Space.HubId,
                    CommunityId = p.Discussion.Space.Hub.CommunityId })
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Reported post not found");
            reportedPostId = post.PostId;
            spaceId = post.SpaceId;
            hubId = post.HubId;
            communityId = post.CommunityId;
        }
        else if (!string.IsNullOrEmpty(reportedDiscussionPublicId))
        {
            var discussion = await _context.Discussions
                .Where(d => d.PublicId == reportedDiscussionPublicId)
                .Select(d => new {
                    DiscussionId = d.Id,
                    SpaceId = d.SpaceId,
                    HubId = d.Space.HubId,
                    CommunityId = d.Space.Hub.CommunityId })
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Reported discussion not found");
            reportedDiscussionId = discussion.DiscussionId;
            spaceId = discussion.SpaceId;
            hubId = discussion.HubId;
            communityId = discussion.CommunityId;
        }
        else if (!string.IsNullOrEmpty(reportedUserPublicId))
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == reportedUserPublicId)
                ?? throw new InvalidOperationException("Reported user not found");
            reportedUserId = user.Id;
        }

        int? reasonId = null;

        if (!string.IsNullOrEmpty(reasonPublicId))
        {
            var reason = await _context.ReportReasons.FirstOrDefaultAsync(r => r.PublicId == reasonPublicId);
            reasonId = reason?.Id;
        }

        var report = new ReportDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            ReporterUserId = reporter.Id,
            ReportedPostId = reportedPostId,
            ReportedDiscussionId = reportedDiscussionId,
            ReportedUserId = reportedUserId,
            ReasonId = reasonId,
            Details = details,
            CreatedAt = DateTime.UtcNow,
            SpaceId = spaceId,
            HubId = hubId,
            CommunityId = communityId,

            StatusId = (int)ReportStatusEnum.Pending,
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        return new ReportDto(
            report.PublicId,
            "Pending",
            reporterUserPublicId,
            reportedPostPublicId,
            reportedDiscussionPublicId,
            reportedUserPublicId,
            reasonPublicId,
            details,
            report.CreatedAt,
            null, null, null);
    }

    public async Task ResolveReportAsync(
        string reportPublicId,
        string resolvedByUserPublicId,
        string? resolutionNote,
        bool dismiss)
    {
        var report = await _context.Reports
            .AsTracking()
            .Include(r => r.Community)
            .Include(r => r.Hub)
            .Include(r => r.Space)
            .FirstOrDefaultAsync(r => r.PublicId == reportPublicId)
            ?? throw new InvalidOperationException("Report not found");

        var resolver = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == resolvedByUserPublicId)
            ?? throw new InvalidOperationException("Resolver not found");

        report.StatusId = dismiss ? (int)ReportStatusEnum.Dismissed : (int)ReportStatusEnum.Resolved;
        report.ResolvedAt = DateTime.UtcNow;
        report.ResolvedByUserId = resolver.Id;
        report.ResolutionNote = resolutionNote;

        await _context.SaveChangesAsync();

        await LogModerationActionAsync(
            resolvedByUserPublicId,
            dismiss ? "DismissReport" : "ResolveReport",
            communityPublicId: report.Community?.PublicId,
            hubPublicId: report.Hub?.PublicId,
            spacePublicId: report.Space?.PublicId,
            reason: resolutionNote);
    }

    public async Task<ReportCommentDto> AddReportCommentAsync(
        string reportPublicId,
        string authorUserPublicId,
        string content)
    {
        var report = await _context.Reports.FirstOrDefaultAsync(r => r.PublicId == reportPublicId)
            ?? throw new InvalidOperationException("Report not found");

        var author = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == authorUserPublicId)
            ?? throw new InvalidOperationException("Author not found");

        var comment = new ReportCommentDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            ReportId = report.Id,
            AuthorUserId = author.Id,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _context.ReportComments.Add(comment);
        await _context.SaveChangesAsync();

        return new ReportCommentDto(
            comment.PublicId,
            authorUserPublicId,
            author.DisplayName,
            content,
            comment.CreatedAt,
            null);
    }

    // ==================== Report Reasons ====================

    public async Task<IEnumerable<ReportReasonDto>> GetReportReasonsForScopeAsync(
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null)
    {
        var query = _context.ReportReasons.AsQueryable();

        query = query.Where(rr =>
            (rr.CommunityId == null && rr.HubId == null && rr.SpaceId == null)
            || (!string.IsNullOrEmpty(communityPublicId) && rr.Community != null && rr.Community.PublicId == communityPublicId)
            || (!string.IsNullOrEmpty(hubPublicId) && rr.Hub != null && rr.Hub.PublicId == hubPublicId)
            || (!string.IsNullOrEmpty(spacePublicId) && rr.Space != null && rr.Space.PublicId == spacePublicId));

        return await query
            .OrderBy(rr => rr.DisplayOrder)
            .ThenBy(rr => rr.Name)
            .Select(rr => new ReportReasonDto(
                rr.PublicId,
                rr.Name,
                rr.Description,
                rr.Community != null ? rr.Community.PublicId : null,
                rr.Hub != null ? rr.Hub.PublicId : null,
                rr.Space != null ? rr.Space.PublicId : null,
                rr.DisplayOrder))
            .ToListAsync();
    }

    public async Task<IEnumerable<ReportReasonDto>> GetGlobalReportReasonsAsync() => await _context.ReportReasons
        .Where(rr =>
            rr.CommunityId == null
            && rr.HubId == null
            && rr.SpaceId == null)
        .OrderBy(rr => rr.DisplayOrder)
        .ThenBy(rr => rr.Name)
        .Select(rr => new ReportReasonDto(
            rr.PublicId,
            rr.Name,
            rr.Description,
            null, null, null,
            rr.DisplayOrder))
        .ToListAsync();

    // ==================== Scope-Based Queries ====================

    public async Task<IEnumerable<UserBanDto>> GetActiveBansForScopeAsync(
        string scopeType, string scopePublicId)
    {
        var now = DateTime.UtcNow;

        var query = scopeType switch
        {
            "Community" => _context.UserBans
                .Where(ub => ub.Community != null && ub.Community.PublicId == scopePublicId),
            "Hub" => _context.UserBans
                .Where(ub => ub.Hub != null && ub.Hub.PublicId == scopePublicId),
            "Space" => _context.UserBans
                .Where(ub => ub.Space != null && ub.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        return await query
            .Where(ub => ub.UnbannedAt == null && (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .OrderByDescending(ub => ub.BannedAt)
            .Select(ub => new UserBanDto(
                ub.PublicId,
                ub.User.PublicId,
                ub.User.DisplayName,
                ((BanTypeEnum)ub.BanTypeId).ToString(),
                ub.Community != null ? ub.Community.PublicId : null,
                ub.Community != null ? ub.Community.Name : null,
                ub.Hub != null ? ub.Hub.PublicId : null,
                ub.Hub != null ? ub.Hub.Name : null,
                ub.Space != null ? ub.Space.PublicId : null,
                ub.Space != null ? ub.Space.Name : null,
                ub.Reason,
                ub.BannedAt,
                ub.ExpiresAt,
                ub.BannedByUser.PublicId,
                ub.BannedByUser.DisplayName,
                ub.UnbannedAt,
                null, null))
            .ToListAsync();
    }

    public async Task<int> GetActiveBanCountForScopeAsync(string scopeType, string scopePublicId)
    {
        var now = DateTime.UtcNow;

        var query = scopeType switch
        {
            "Community" => _context.UserBans
                .Where(ub => ub.Community != null && ub.Community.PublicId == scopePublicId),
            "Hub" => _context.UserBans
                .Where(ub => ub.Hub != null && ub.Hub.PublicId == scopePublicId),
            "Space" => _context.UserBans
                .Where(ub => ub.Space != null && ub.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        return await query
            .Where(ub => ub.UnbannedAt == null && (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .CountAsync();
    }

    public async Task<IEnumerable<UserRoleDto>> GetActiveRolesForScopeAsync(
        string scopeType, string scopePublicId)
    {
        var query = scopeType switch
        {
            "Community" => _context.UserRoles
                .Where(ur => ur.Community != null && ur.Community.PublicId == scopePublicId && ur.RevokedAt == null),
            "Hub" => _context.UserRoles
                .Where(ur => ur.Hub != null && ur.Hub.PublicId == scopePublicId && ur.RevokedAt == null),
            "Space" => _context.UserRoles
                .Where(ur => ur.Space != null && ur.Space.PublicId == scopePublicId && ur.RevokedAt == null),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        return await query
            .Select(ur => new UserRoleDto(
                ur.PublicId,
                ur.User.PublicId,
                ur.User.DisplayName,
                ((UserRoleTypeEnum)ur.RoleId).ToString(),
                ur.Community != null ? ur.Community.PublicId : null,
                ur.Community != null ? ur.Community.Name : null,
                ur.Hub != null ? ur.Hub.PublicId : null,
                ur.Hub != null ? ur.Hub.Name : null,
                ur.Space != null ? ur.Space.PublicId : null,
                ur.Space != null ? ur.Space.Name : null,
                ur.AssignedByUser.PublicId,
                ur.AssignedByUser.DisplayName,
                ur.AssignedAt,
                ur.RevokedAt))
            .ToListAsync();
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForScopeAsync(
        string scopeType, string scopePublicId, int? statusId, int offset, int pageSize)
    {
        return scopeType switch
        {
            "Community" => await GetReportsForCommunityAsync(scopePublicId, statusId, offset, pageSize),
            "Hub" => await GetReportsForHubAsync(scopePublicId, statusId, offset, pageSize),
            "Space" => await GetReportsForSpaceAsync(scopePublicId, statusId, offset, pageSize),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };
    }

    public async Task<int> GetOpenReportCountForScopeAsync(string scopeType, string scopePublicId)
    {
        var result = await GetReportsForScopeAsync(scopeType, scopePublicId,
            (int)ReportStatusEnum.Pending, 0, 1);
        // We need count, not items — use a simpler query
        var query = scopeType switch
        {
            "Community" => _context.Reports
                .Where(r => r.Community != null && r.Community.PublicId == scopePublicId),
            "Hub" => _context.Reports
                .Where(r => (r.Hub != null && r.Hub.PublicId == scopePublicId)
                            || (r.Space != null && r.Space.Hub.PublicId == scopePublicId)),
            "Space" => _context.Reports
                .Where(r => r.Space != null && r.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        return await query.Where(r => r.StatusId == (int)ReportStatusEnum.Pending).CountAsync();
    }

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogForScopeAsync(
        string scopeType, string scopePublicId, int offset, int pageSize)
    {
        return scopeType switch
        {
            "Community" => await GetModerationLogForCommunityAsync(scopePublicId, offset, pageSize),
            "Hub" => await GetModerationLogForHubAsync(scopePublicId, offset, pageSize),
            "Space" => await GetModerationLogForSpaceAsync(scopePublicId, offset, pageSize),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };
    }

    public async Task<IEnumerable<ReportReasonDto>> GetReportReasonsForExactScopeAsync(
        string scopeType, string scopePublicId)
    {
        var query = scopeType switch
        {
            "Community" => _context.ReportReasons
                .Where(rr => rr.Community != null && rr.Community.PublicId == scopePublicId
                             && rr.HubId == null && rr.SpaceId == null),
            "Hub" => _context.ReportReasons
                .Where(rr => rr.Hub != null && rr.Hub.PublicId == scopePublicId
                             && rr.SpaceId == null),
            "Space" => _context.ReportReasons
                .Where(rr => rr.Space != null && rr.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        return await query
            .OrderBy(rr => rr.DisplayOrder)
            .ThenBy(rr => rr.Name)
            .Select(rr => new ReportReasonDto(
                rr.PublicId, rr.Name, rr.Description,
                rr.Community != null ? rr.Community.PublicId : null,
                rr.Hub != null ? rr.Hub.PublicId : null,
                rr.Space != null ? rr.Space.PublicId : null,
                rr.DisplayOrder))
            .ToListAsync();
    }

    public async Task ReplaceReportReasonsForScopeAsync(
        string scopeType, string scopePublicId, string userPublicId,
        IEnumerable<(string Name, string? Description, int DisplayOrder)> reasons)
    {
        // Soft-delete existing reasons at this exact scope
        var existing = scopeType switch
        {
            "Community" => await _context.ReportReasons
                .Where(rr => rr.Community != null && rr.Community.PublicId == scopePublicId
                             && rr.HubId == null && rr.SpaceId == null)
                .ToListAsync(),
            "Hub" => await _context.ReportReasons
                .Where(rr => rr.Hub != null && rr.Hub.PublicId == scopePublicId
                             && rr.SpaceId == null)
                .ToListAsync(),
            "Space" => await _context.ReportReasons
                .Where(rr => rr.Space != null && rr.Space.PublicId == scopePublicId)
                .ToListAsync(),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var now = DateTime.UtcNow;

        foreach (var reason in existing)
        {
            reason.IsDeleted = true;
            reason.DeletedAt = now;
        }

        // Resolve scope entity ID and user ID
        int? communityId = null, hubId = null, spaceId = null;

        switch (scopeType)
        {
            case "Community":
                communityId = await _context.Communities
                    .Where(c => c.PublicId == scopePublicId)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync();
                break;
            case "Hub":
                hubId = await _context.Hubs
                    .Where(h => h.PublicId == scopePublicId)
                    .Select(h => (int?)h.Id)
                    .FirstOrDefaultAsync();
                break;
            case "Space":
                spaceId = await _context.Spaces
                    .Where(s => s.PublicId == scopePublicId)
                    .Select(s => (int?)s.Id)
                    .FirstOrDefaultAsync();
                break;
        }

        var userId = await _context.Users
            .Where(u => u.PublicId == userPublicId)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        // Insert new reasons
        foreach (var r in reasons)
        {
            _context.ReportReasons.Add(new ReportReasonDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Name = r.Name,
                Description = r.Description,
                CommunityId = communityId,
                HubId = hubId,
                SpaceId = spaceId,
                CreatedByUserId = userId,
                CreatedAt = now,
                DisplayOrder = r.DisplayOrder
            });
        }

        await _context.SaveChangesAsync();
    }

    // ==================== Moderation Log ====================

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogForCommunityAsync(
        string communityPublicId,
        int offset,
        int pageSize)
    {
        var query = _context.ModerationLogs
            .Where(ml => ml.Community != null && ml.Community.PublicId == communityPublicId);

        return await GetPagedLogsAsync(query, offset, pageSize);
    }

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogForHubAsync(
        string hubPublicId,
        int offset,
        int pageSize)
    {
        var query = _context.ModerationLogs
            .Where(ml => ml.Hub != null && ml.Hub.PublicId == hubPublicId);

        return await GetPagedLogsAsync(query, offset, pageSize);
    }

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogForSpaceAsync(
        string spacePublicId,
        int offset,
        int pageSize)
    {
        var query = _context.ModerationLogs
            .Where(ml => ml.Space != null && ml.Space.PublicId == spacePublicId);

        return await GetPagedLogsAsync(query, offset, pageSize);
    }

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogByActorAsync(
        string actorUserPublicId,
        int offset,
        int pageSize)
    {
        var query = _context.ModerationLogs
            .Where(ml => ml.ActorUser.PublicId == actorUserPublicId);

        return await GetPagedLogsAsync(query, offset, pageSize);
    }

    private async Task<PagedResult<ModerationLogDto>> GetPagedLogsAsync(
        IQueryable<ModerationLogDatabaseEntity> query,
        int offset,
        int pageSize)
    {
        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(ml => ml.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(ml => new ModerationLogDto(
                ml.PublicId,
                ml.ActorUser.PublicId,
                ml.ActorUser.DisplayName,
                ((ModerationActionEnum)ml.ActionId).ToString(),
                ml.TargetPost != null ? ml.TargetPost.PublicId : null,
                ml.TargetDiscussion != null ? ml.TargetDiscussion.PublicId : null,
                ml.TargetDiscussion != null ? ml.TargetDiscussion.Title : null,
                ml.TargetUser != null ? ml.TargetUser.PublicId : null,
                ml.TargetUser != null ? ml.TargetUser.DisplayName : null,
                ml.Community != null ? ml.Community.PublicId : null,
                ml.Community != null ? ml.Community.Name : null,
                ml.Hub != null ? ml.Hub.PublicId : null,
                ml.Hub != null ? ml.Hub.Name : null,
                ml.Space != null ? ml.Space.PublicId : null,
                ml.Space != null ? ml.Space.Name : null,
                ml.Details,
                ml.Reason,
                ml.CreatedAt))
            .ToListAsync();

        return new PagedResult<ModerationLogDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = offset + items.Count < totalCount
        };
    }

    public async Task LogModerationActionAsync(
        string actorUserPublicId,
        string action,
        string? targetPostPublicId = null,
        string? targetDiscussionPublicId = null,
        string? targetUserPublicId = null,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        string? details = null,
        string? reason = null)
    {
        var actor = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == actorUserPublicId);

        if (actor is null) return;

        int? targetPostId = null, targetDiscussionId = null, targetUserId = null;
        int? communityId = null, hubId = null, spaceId = null;

        if (!string.IsNullOrEmpty(targetPostPublicId))
            targetPostId = (await _context.Posts.FirstOrDefaultAsync(p => p.PublicId == targetPostPublicId))?.Id;

        if (!string.IsNullOrEmpty(targetDiscussionPublicId))
            targetDiscussionId = (await _context.Discussions.FirstOrDefaultAsync(d => d.PublicId == targetDiscussionPublicId))?.Id;

        if (!string.IsNullOrEmpty(targetUserPublicId))
            targetUserId = (await _context.Users.FirstOrDefaultAsync(u => u.PublicId == targetUserPublicId))?.Id;

        if (!string.IsNullOrEmpty(communityPublicId))
            communityId = (await _context.Communities.FirstOrDefaultAsync(c => c.PublicId == communityPublicId))?.Id;

        if (!string.IsNullOrEmpty(hubPublicId))
            hubId = (await _context.Hubs.FirstOrDefaultAsync(h => h.PublicId == hubPublicId))?.Id;

        if (!string.IsNullOrEmpty(spacePublicId))
            spaceId = (await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == spacePublicId))?.Id;

        var log = new ModerationLogDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            ActorUserId = actor.Id,
            TargetPostId = targetPostId,
            TargetDiscussionId = targetDiscussionId,
            TargetUserId = targetUserId,
            CommunityId = communityId,
            HubId = hubId,
            SpaceId = spaceId,
            Details = details,
            Reason = reason,
            CreatedAt = DateTime.UtcNow,

            ActionId = action switch
            {
                "DeletePost" => (int)ModerationActionEnum.DeletePost,
                "DeleteDiscussion" => (int)ModerationActionEnum.DeleteDiscussion,
                "BanUser" => (int)ModerationActionEnum.BanUser,
                "UnbanUser" => (int)ModerationActionEnum.UnbanUser,
                "AssignRole" => (int)ModerationActionEnum.AssignRole,
                "RevokeRole" => (int)ModerationActionEnum.RevokeRole,
                "ResolveReport" => (int)ModerationActionEnum.ResolveReport,
                "DismissReport" => (int)ModerationActionEnum.DismissReport,
                "EditPost" => (int)ModerationActionEnum.EditPost,
                "LockDiscussion" => (int)ModerationActionEnum.LockDiscussion,
                "UnlockDiscussion" => (int)ModerationActionEnum.LockDiscussion, // Using same enum value for now
                _ => throw new ArgumentException($"Unknown moderation action: {action}", nameof(action))
            },
        };

        _context.ModerationLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    // ==================== Content Moderation ====================

    public async Task ModeratorDeletePostAsync(
        string postPublicId,
        string moderatorPublicId,
        string? reason)
    {
        var post = await _context.Posts
            .AsTracking()
            .Include(p => p.Discussion)
                .ThenInclude(d => d.Space)
                    .ThenInclude(s => s.Hub)
            .FirstOrDefaultAsync(p => p.PublicId == postPublicId)
            ?? throw new InvalidOperationException("Post not found");

        var canModerate = await CanModerateAsync(
            moderatorPublicId,
            post.Discussion.Space.Hub.Community?.PublicId,
            post.Discussion.Space.Hub.PublicId,
            post.Discussion.Space.PublicId);

        if (!canModerate)
            throw new InvalidOperationException("You don't have permission to delete this post");

        post.IsDeleted = true;
        post.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await LogModerationActionAsync(
            moderatorPublicId,
            "DeletePost",
            targetPostPublicId: postPublicId,
            communityPublicId: post.Discussion.Space.Hub.Community?.PublicId,
            hubPublicId: post.Discussion.Space.Hub.PublicId,
            spacePublicId: post.Discussion.Space.PublicId,
            reason: reason);
    }

    public async Task ModeratorDeleteDiscussionAsync(
        string discussionPublicId,
        string moderatorPublicId,
        string? reason)
    {
        var discussion = await _context.Discussions
            .AsTracking()
            .Include(d => d.Space)
                .ThenInclude(s => s.Hub)
                    .ThenInclude(h => h.Community)
            .FirstOrDefaultAsync(d => d.PublicId == discussionPublicId)
            ?? throw new InvalidOperationException("Discussion not found");

        var canModerate = await CanModerateAsync(
            moderatorPublicId,
            discussion.Space.Hub.Community?.PublicId,
            discussion.Space.Hub.PublicId,
            discussion.Space.PublicId);

        if (!canModerate)
            throw new InvalidOperationException("You don't have permission to delete this discussion");

        discussion.IsDeleted = true;
        discussion.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await LogModerationActionAsync(
            moderatorPublicId,
            "DeleteDiscussion",
            targetDiscussionPublicId: discussionPublicId,
            communityPublicId: discussion.Space.Hub.Community?.PublicId,
            hubPublicId: discussion.Space.Hub.PublicId,
            spacePublicId: discussion.Space.PublicId,
            reason: reason);
    }

    public async Task LockDiscussionAsync(
        string discussionPublicId,
        string moderatorPublicId,
        string? reason)
    {
        var discussion = await _context.Discussions
            .AsTracking()
            .Include(d => d.Space)
                .ThenInclude(s => s.Hub)
                    .ThenInclude(h => h.Community)
            .FirstOrDefaultAsync(d => d.PublicId == discussionPublicId)
            ?? throw new InvalidOperationException("Discussion not found");

        var canModerate = await CanModerateAsync(
            moderatorPublicId,
            discussion.Space.Hub.Community?.PublicId,
            discussion.Space.Hub.PublicId,
            discussion.Space.PublicId);

        if (!canModerate)
            throw new InvalidOperationException("You don't have permission to lock this discussion");

        discussion.IsLocked = true;

        await _context.SaveChangesAsync();

        await LogModerationActionAsync(
            moderatorPublicId,
            "LockDiscussion",
            targetDiscussionPublicId: discussionPublicId,
            communityPublicId: discussion.Space.Hub.Community?.PublicId,
            hubPublicId: discussion.Space.Hub.PublicId,
            spacePublicId: discussion.Space.PublicId,
            reason: reason);
    }

    public async Task UnlockDiscussionAsync(
        string discussionPublicId,
        string moderatorPublicId)
    {
        var discussion = await _context.Discussions
            .AsTracking()
            .Include(d => d.Space)
                .ThenInclude(s => s.Hub)
                    .ThenInclude(h => h.Community)
            .FirstOrDefaultAsync(d => d.PublicId == discussionPublicId)
            ?? throw new InvalidOperationException("Discussion not found");

        var canModerate = await CanModerateAsync(
            moderatorPublicId,
            discussion.Space.Hub.Community?.PublicId,
            discussion.Space.Hub.PublicId,
            discussion.Space.PublicId);

        if (!canModerate)
            throw new InvalidOperationException("You don't have permission to unlock this discussion");

        discussion.IsLocked = false;

        await _context.SaveChangesAsync();

        await LogModerationActionAsync(
            moderatorPublicId,
            "UnlockDiscussion",
            targetDiscussionPublicId: discussionPublicId,
            communityPublicId: discussion.Space.Hub.Community?.PublicId,
            hubPublicId: discussion.Space.Hub.PublicId,
            spacePublicId: discussion.Space.PublicId);
    }
}
