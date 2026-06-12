namespace Snakk.Application.UseCases;

using Snakk.Domain;
using Snakk.Domain.ValueObjects;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Domain.Repositories;
using Snakk.Shared.Models;

public class ModerationUseCase(
    IModerationRepository moderationRepository,
    IRevocationCache revocationCache,
    IAuthVersionCache authVersionCache,
    IUserRepository userRepository) : UseCaseBase
{
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
            var canAssign = await moderationRepository.CanAdministerAsync(
                assignedByUserPublicId, communityPublicId, hubPublicId, spacePublicId);

            if (!canAssign)
                return Result<UserRoleDto>.Failure("You don't have permission to assign roles at this scope");

            var role = await moderationRepository.AssignRoleAsync(
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
            var role = await moderationRepository.GetRoleByPublicIdAsync(rolePublicId);

            if (role is null)
                return Result.Failure("Role assignment not found");

            if (role.RevokedAt is not null)
                return Result.Failure("Role already revoked");

            // Check if revoker has permission
            var canRevoke = await moderationRepository.CanAdministerAsync(
                revokedByUserPublicId, role.CommunityPublicId, role.HubPublicId, role.SpacePublicId);

            if (!canRevoke)
                return Result.Failure("You don't have permission to revoke roles at this scope");

            await moderationRepository.RevokeRoleAsync(rolePublicId, revokedByUserPublicId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<IEnumerable<UserRoleDto>> GetUserRolesAsync(string userPublicId) =>
        await moderationRepository.GetActiveRolesForUserAsync(userPublicId);

    public async Task<bool> CanModerateAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null) =>
        await moderationRepository.CanModerateAsync(userPublicId, communityPublicId, hubPublicId, spacePublicId);

    public async Task<bool> CanAdministerAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null) =>
        await moderationRepository.CanAdministerAsync(userPublicId, communityPublicId, hubPublicId, spacePublicId);

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
            var canBan = await moderationRepository.CanModerateAsync(
                bannedByUserPublicId, communityPublicId, hubPublicId, spacePublicId);

            if (!canBan)
                return Result<UserBanDto>.Failure("You don't have permission to ban users at this scope");

            // Check if target is a moderator at this scope
            var targetCanModerate = await moderationRepository.CanModerateAsync(
                targetUserPublicId, communityPublicId, hubPublicId, spacePublicId);

            if (targetCanModerate)
                return Result<UserBanDto>.Failure("Cannot ban a user with moderator privileges at this scope");

            var ban = await moderationRepository.BanUserAsync(
                targetUserPublicId, banType, communityPublicId, hubPublicId, spacePublicId, reason, expiresAt, bannedByUserPublicId);

            // For global bans, immediately revoke all active sessions
            if (communityPublicId is null && hubPublicId is null && spacePublicId is null)
            {
                await revocationCache.RevokeUserAsync(targetUserPublicId);

                var user = await userRepository.GetByPublicIdAsync(Domain.ValueObjects.UserId.From(targetUserPublicId));
                if (user is not null)
                {
                    user.IncrementAuthVersion();
                    await userRepository.UpdateAsync(user);
                    await authVersionCache.InvalidateAsync(targetUserPublicId);
                }
            }

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
            var ban = await moderationRepository.GetBanByPublicIdAsync(banPublicId);

            if (ban is null)
                return Result.Failure("Ban not found");

            if (ban.UnbannedAt is not null)
                return Result.Failure("User already unbanned");

            // Check if unbanner has permission
            var canUnban = await moderationRepository.CanModerateAsync(
                unbannedByUserPublicId, ban.CommunityPublicId, ban.HubPublicId, ban.SpacePublicId);

            if (!canUnban)
                return Result.Failure("You don't have permission to unban users at this scope");

            await moderationRepository.UnbanUserAsync(banPublicId, unbannedByUserPublicId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<bool> IsUserBannedAsync(string userPublicId, string? spacePublicId = null) =>
        await moderationRepository.IsUserBannedAsync(userPublicId, spacePublicId: spacePublicId);

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
            if (string.IsNullOrEmpty(reportedPostPublicId)
                && string.IsNullOrEmpty(reportedDiscussionPublicId)
                && string.IsNullOrEmpty(reportedUserPublicId))
            {
                return Result<ReportDto>.Failure("Must specify content to report");
            }

            var report = await moderationRepository.CreateReportAsync(
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
            var report = await moderationRepository.GetReportByPublicIdAsync(reportPublicId);

            if (report is null)
                return Result.Failure("Report not found");

            if (report.Status != "Pending")
                return Result.Failure("Report is not pending");

            await moderationRepository.ResolveReportAsync(
                reportPublicId,
                resolvedByUserPublicId,
                resolutionNote,
                dismiss);

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
            var comment = await moderationRepository.AddReportCommentAsync(reportPublicId, authorUserPublicId, content);
            return Result<ReportCommentDto>.Success(comment);
        }
        catch (Exception ex)
        {
            return Result<ReportCommentDto>.Failure(ex.Message);
        }
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForModeratorAsync(
        string moderatorPublicId,
        int? statusId,
        int offset,
        int pageSize) =>
        await moderationRepository.GetReportsForModeratorAsync(moderatorPublicId, statusId, offset, pageSize);

    public async Task<ReportDetailDto?> GetReportDetailAsync(string reportPublicId) =>
        await moderationRepository.GetReportDetailByPublicIdAsync(reportPublicId);

    public async Task<int> GetPendingReportCountAsync(string moderatorPublicId) =>
        await moderationRepository.GetPendingReportCountForModeratorAsync(moderatorPublicId);

    // ==================== Content Moderation ====================

    public async Task<Result> ModeratorDeletePostAsync(string postPublicId, string moderatorPublicId, string? reason, CancellationToken ct = default)
    {
        try
        {
            await moderationRepository.ModeratorDeletePostAsync(postPublicId, moderatorPublicId, reason, ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> ModeratorDeleteDiscussionAsync(string discussionPublicId, string moderatorPublicId, string? reason, CancellationToken ct = default)
    {
        try
        {
            await moderationRepository.ModeratorDeleteDiscussionAsync(discussionPublicId, moderatorPublicId, reason, ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> LockDiscussionAsync(string discussionPublicId, string moderatorPublicId, string? reason, CancellationToken ct = default)
    {
        try
        {
            await moderationRepository.LockDiscussionAsync(discussionPublicId, moderatorPublicId, reason, ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> UnlockDiscussionAsync(string discussionPublicId, string moderatorPublicId, CancellationToken ct = default)
    {
        try
        {
            await moderationRepository.UnlockDiscussionAsync(discussionPublicId, moderatorPublicId, ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    // ==================== Report Reasons ====================

    public async Task<IEnumerable<ReportReasonDto>> GetReportReasonsAsync(string? spacePublicId = null) =>
        await moderationRepository.GetReportReasonsForScopeAsync(spacePublicId: spacePublicId);

    // ==================== Moderation Log ====================

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogAsync(
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        int offset,
        int pageSize)
    {
        if (!string.IsNullOrEmpty(spacePublicId))
            return await moderationRepository.GetModerationLogForSpaceAsync(spacePublicId, offset, pageSize);

        if (!string.IsNullOrEmpty(hubPublicId))
            return await moderationRepository.GetModerationLogForHubAsync(hubPublicId, offset, pageSize);

        if (!string.IsNullOrEmpty(communityPublicId))
            return await moderationRepository.GetModerationLogForCommunityAsync(communityPublicId, offset, pageSize);

        return new PagedResult<ModerationLogDto> { Items = [], Offset = offset, PageSize = pageSize, HasMoreItems = false };
    }
}
