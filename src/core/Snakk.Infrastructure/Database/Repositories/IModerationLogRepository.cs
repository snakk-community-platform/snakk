namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface IModerationLogRepository : IGenericDatabaseRepository<ModerationLogDatabaseEntity>
{
    Task<ModerationLogDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Get moderation history for a scope
    /// </summary>
    Task<PagedResult<ModerationLogDto>> GetLogsForCommunityAsync(int communityId, int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<ModerationLogDto>> GetLogsForHubAsync(int hubId, int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<ModerationLogDto>> GetLogsForSpaceAsync(int spaceId, int offset, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Get moderation actions by a specific moderator
    /// </summary>
    Task<PagedResult<ModerationLogDto>> GetLogsByActorAsync(int actorUserId, int offset, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Get moderation actions targeting a specific user
    /// </summary>
    Task<PagedResult<ModerationLogDto>> GetLogsForTargetUserAsync(int targetUserId, int offset, int pageSize, CancellationToken ct = default);
}
