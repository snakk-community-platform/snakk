namespace Snakk.Application.UseCases;

using Snakk.Domain;
using Snakk.Domain.ValueObjects;
using Snakk.Application.Repositories;
using Snakk.Shared.Models;

public class ModerationUseCase(IModerationRepository moderationRepository) : UseCaseBase
{
    private readonly IModerationRepository _moderationRepository = moderationRepository;

    // ==================== Role Management ====================

    public async Task<Result<UserRoleDto>> AssignRoleAsync(
        string targetUserPublicId,
        UserRoleType roleType,
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        string assignedByUserPublicId)
    {
        try
        {
            // Check if assigner has permission
            var canAssign = await _moderationRepository.CanAdministerAsync(
                assignedByUserPublicId, communityPublicId, hubPublicId, spacePublicId);
            
            if (!canAssign)
                return Result<UserRoleDto>.Failure("You don't have permission to assign roles at this scope");

            var role = await _moderationRepository.AssignRoleAsync(
                targetUserPublicId, roleType, communityPublicId, hubPublicId, spacePublicId, assignedByUserPublicId);

            return Result<UserRoleDto>.Success(role);
        }
        catch (Exception ex)
        {
            return Result<UserRoleDto>.Failure(ex.Message);
        }
    }

    public async Task<Result> RevokeRoleAsync(string rolePublicId, string revokedByUserPublicId)
    {
        try
        {
            var role = await _moderationRepository.GetRoleByPublicIdAsync(rolePublicId);
            if (role == null)
                return Result.Failure("Role assignment not found");

            if (role.RevokedAt != null)
                return Result.Failure("Role already revoked");

            // Check if revoker has permission
            var canRevoke = await _moderationRepository.CanAdministerAsync(
                revokedByUserPublicId, role.CommunityPublicId, role.HubPublicId, role.SpacePublicId);
            
            if (!canRevoke)
                return Result.Failure("You don't have permission to revoke roles at this scope");

            await _moderationRepository.RevokeRoleAsync(rolePublicId, revokedByUserPublicId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<IEnumerable<UserRoleDto>> GetUserRolesAsync(string userPublicId)
    {
        return await _moderationRepository.GetActiveRolesForUserAsync(userPublicId);
    }

    public async Task<bool> CanModerateAsync(string userPublicId, string? communityPublicId = null, string? hubPublicId = null, string? spacePublicId = null)
    {
        return await _moderationRepository.CanModerateAsync(userPublicId, communityPublicId, hubPublicId, spacePublicId);
    }

    public async Task<bool> CanAdministerAsync(string userPublicId, string? communityPublicId = null, string? hubPublicId = null, string? spacePublicId = null)
    {
        return await _moderationRepository.CanAdministerAsync(userPublicId, communityPublicId, hubPublicId, spacePublicId);
    }

    // ==================== Ban Management ====================

    public async Task<Result<UserBanDto>> BanUserAsync(
        string targetUserPublicId,
        BanType banType,
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        string? reason,
        DateTime? expiresAt,
        string bannedByUserPublicId)
    {
        try
        {
            // Check if banner has permission
            var canBan = await _moderationRepository.CanModerateAsync(
                bannedByUserPublicId, communityPublicId, hubPublicId, spacePublicId);
            
            if (!canBan)
                return Result<UserBanDto>.Failure("You don't have permission to ban users at this scope");

            // Check if target is a moderator at this scope
            var targetCanModerate = await _moderationRepository.CanModerateAsync(
                targetUserPublicId, communityPublicId, hubPublicId, spacePublicId);
            
            if (targetCanModerate)
                return Result<UserBanDto>.Failure("Cannot ban a user with moderator privileges at this scope");

            var ban = await _moderationRepository.BanUserAsync(
                targetUserPublicId, banType, communityPublicId, hubPublicId, spacePublicId, reason, expiresAt, bannedByUserPublicId);

            return Result<UserBanDto>.Success(ban);
        }
        catch (Exception ex)
        {
            return Result<UserBanDto>.Failure(ex.Message);
        }
    }

    public async Task<Result> UnbanUserAsync(string banPublicId, string unbannedByUserPublicId)
    {
        try
        {
            var ban = await _moderationRepository.GetBanByPublicIdAsync(banPublicId);
            if (ban == null)
                return Result.Failure("Ban not found");

            if (ban.UnbannedAt != null)
                return Result.Failure("User already unbanned");

            // Check if unbanner has permission
            var canUnban = await _moderationRepository.CanModerateAsync(
                unbannedByUserPublicId, ban.CommunityPublicId, ban.HubPublicId, ban.SpacePublicId);
            
            if (!canUnban)
                return Result.Failure("You don't have permission to unban users at this scope");

            await _moderationRepository.UnbanUserAsync(banPublicId, unbannedByUserPublicId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<bool> IsUserBannedAsync(string userPublicId, string? spacePublicId = null)
    {
        return await _moderationRepository.IsUserBannedAsync(userPublicId, spacePublicId: spacePublicId);
    }

    // ==================== Report Management ====================

    public async Task<Result<ReportDto>> CreateReportAsync(
        string reporterUserPublicId,
        string? reportedPostPublicId,
        string? reportedDiscussionPublicId,
        string? reportedUserPublicId,
        string? reasonPublicId,
        string? details)
    {
        try
        {
            if (string.IsNullOrEmpty(reportedPostPublicId) && 
                string.IsNullOrEmpty(reportedDiscussionPublicId) && 
                string.IsNullOrEmpty(reportedUserPublicId))
            {
                return Result<ReportDto>.Failure("Must specify content to report");
            }

            var report = await _moderationRepository.CreateReportAsync(
                reporterUserPublicId, reportedPostPublicId, reportedDiscussionPublicId, reportedUserPublicId, reasonPublicId, details);

            return Result<ReportDto>.Success(report);
        }
        catch (Exception ex)
        {
            return Result<ReportDto>.Failure(ex.Message);
        }
    }

    public async Task<Result> ResolveReportAsync(string reportPublicId, string resolvedByUserPublicId, string? resolutionNote, bool dismiss = false)
    {
        try
        {
            var report = await _moderationRepository.GetReportByPublicIdAsync(reportPublicId);
            if (report == null)
                return Result.Failure("Report not found");

            if (report.Status != "Pending")
                return Result.Failure("Report is not pending");

            await _moderationRepository.ResolveReportAsync(reportPublicId, resolvedByUserPublicId, resolutionNote, dismiss);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result<ReportCommentDto>> AddReportCommentAsync(string reportPublicId, string authorUserPublicId, string content)
    {
        try
        {
            var comment = await _moderationRepository.AddReportCommentAsync(reportPublicId, authorUserPublicId, content);
            return Result<ReportCommentDto>.Success(comment);
        }
        catch (Exception ex)
        {
            return Result<ReportCommentDto>.Failure(ex.Message);
        }
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForModeratorAsync(
        string moderatorPublicId,
        string? status,
        int offset,
        int pageSize)
    {
        return await _moderationRepository.GetReportsForModeratorAsync(moderatorPublicId, status, offset, pageSize);
    }

    public async Task<ReportDetailDto?> GetReportDetailAsync(string reportPublicId)
    {
        return await _moderationRepository.GetReportDetailByPublicIdAsync(reportPublicId);
    }

    public async Task<int> GetPendingReportCountAsync(string moderatorPublicId)
    {
        return await _moderationRepository.GetPendingReportCountForModeratorAsync(moderatorPublicId);
    }

    // ==================== Content Moderation ====================

    public async Task<Result> ModeratorDeletePostAsync(string postPublicId, string moderatorPublicId, string? reason)
    {
        try
        {
            await _moderationRepository.ModeratorDeletePostAsync(postPublicId, moderatorPublicId, reason);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> ModeratorDeleteDiscussionAsync(string discussionPublicId, string moderatorPublicId, string? reason)
    {
        try
        {
            await _moderationRepository.ModeratorDeleteDiscussionAsync(discussionPublicId, moderatorPublicId, reason);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> LockDiscussionAsync(string discussionPublicId, string moderatorPublicId, string? reason)
    {
        try
        {
            await _moderationRepository.LockDiscussionAsync(discussionPublicId, moderatorPublicId, reason);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> UnlockDiscussionAsync(string discussionPublicId, string moderatorPublicId)
    {
        try
        {
            await _moderationRepository.UnlockDiscussionAsync(discussionPublicId, moderatorPublicId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    // ==================== Report Reasons ====================

    public async Task<IEnumerable<ReportReasonDto>> GetReportReasonsAsync(string? spacePublicId = null)
    {
        return await _moderationRepository.GetReportReasonsForScopeAsync(spacePublicId: spacePublicId);
    }

    // ==================== Moderation Log ====================

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogAsync(
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        int offset,
        int pageSize)
    {
        if (!string.IsNullOrEmpty(spacePublicId))
            return await _moderationRepository.GetModerationLogForSpaceAsync(spacePublicId, offset, pageSize);
        
        if (!string.IsNullOrEmpty(hubPublicId))
            return await _moderationRepository.GetModerationLogForHubAsync(hubPublicId, offset, pageSize);
        
        if (!string.IsNullOrEmpty(communityPublicId))
            return await _moderationRepository.GetModerationLogForCommunityAsync(communityPublicId, offset, pageSize);

        return new PagedResult<ModerationLogDto> { Items = [], Offset = offset, PageSize = pageSize, HasMoreItems = false };
    }
}
