using Microsoft.EntityFrameworkCore;
using Snakk.Application.DTOs.Admin;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IAdminModerationDataService"/>.
/// Handles DB queries for UnbanUser, UpdateUserRole, and GetReport admin endpoints.
/// </summary>
public class AdminModerationDataService(
    IDbContextFactory<SnakkDbContext> dbFactory) : IAdminModerationDataService
{
    public async Task<string?> GetActiveBanPublicIdAsync(string userPublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;

        return await db.UserBans
            .Where(b =>
                b.User.PublicId == userPublicId
                && b.UnbannedAt == null
                && (b.ExpiresAt == null || b.ExpiresAt > now))
            .Select(b => b.PublicId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<AdminUserRoleInfoDto?> GetUserRoleInfoAsync(string userPublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var user = await db.Users
            .Where(u => u.PublicId == userPublicId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return null;

        // Find active platform-wide role (no community/hub/space scope, not revoked)
        var platformRole = await db.UserRoles
            .Where(ur =>
                ur.UserId == user.Id
                && ur.CommunityId == null
                && ur.HubId == null
                && ur.SpaceId == null
                && ur.RevokedAt == null)
            .Select(ur => new { ur.PublicId, ur.RoleId })
            .FirstOrDefaultAsync(ct);

        return new AdminUserRoleInfoDto
        {
            DbId = user.Id,
            ActivePlatformRolePublicId = platformRole?.PublicId,
            ActivePlatformRoleId = platformRole?.RoleId
        };
    }

    public async Task<AdminReportDetailDto?> GetReportDetailAsync(string publicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        return await db.Reports
            .Where(r => r.PublicId == publicId)
            .Select(r => new AdminReportDetailDto
            {
                PublicId = r.PublicId,
                ReporterPublicId = r.ReporterUser.PublicId,
                ReporterDisplayName = r.ReporterUser.DisplayName ?? "",
                ReportedUserPublicId = r.ReportedUser != null ? r.ReportedUser.PublicId : null,
                ReportedUserDisplayName = r.ReportedUser != null ? r.ReportedUser.DisplayName : null,
                ReportedPostPublicId = r.ReportedPost != null ? r.ReportedPost.PublicId : null,
                ReportedDiscussionPublicId = r.ReportedDiscussion != null ? r.ReportedDiscussion.PublicId : null,
                ReasonName = r.Reason != null ? r.Reason.Name : null,
                ReasonDescription = r.Reason != null ? r.Reason.Description : null,
                Details = r.Details,
                Status = ((ReportStatusEnum)r.StatusId).ToString(),
                CreatedAt = r.CreatedAt,
                ResolvedAt = r.ResolvedAt,
                ResolverPublicId = r.ResolvedByUser != null ? r.ResolvedByUser.PublicId : null,
                ResolverDisplayName = r.ResolvedByUser != null ? r.ResolvedByUser.DisplayName : null,
                ResolutionNote = r.ResolutionNote
            })
            .FirstOrDefaultAsync(ct);
    }
}
