using Snakk.Shared.Enums;

namespace Snakk.Application.Services;

public interface IAllowedTypesService
{
    /// <summary>
    /// Get the allowed types configured directly on a community.
    /// Empty list = all types allowed.
    /// </summary>
    Task<List<DiscussionTypeEnum>> GetCommunityAllowedTypesAsync(string communityPublicId, CancellationToken ct = default);

    /// <summary>
    /// Get the effective allowed types for a hub (intersection of hub's own list with community's list).
    /// Empty list = all types allowed at that level.
    /// </summary>
    Task<List<DiscussionTypeEnum>> GetHubEffectiveAllowedTypesAsync(string hubPublicId, CancellationToken ct = default);

    /// <summary>
    /// Get the effective allowed types for a space (intersection of space's list with hub's effective list).
    /// This is what the "New Discussion" page should use.
    /// </summary>
    Task<List<DiscussionTypeEnum>> GetSpaceEffectiveAllowedTypesAsync(string spacePublicId, CancellationToken ct = default);

    /// <summary>
    /// Get the parent (community) allowed types for a hub — used by admin UI to grey out disabled types.
    /// </summary>
    Task<List<DiscussionTypeEnum>> GetParentAllowedTypesForHubAsync(string hubPublicId, CancellationToken ct = default);

    /// <summary>
    /// Get the parent (hub effective) allowed types for a space — used by admin UI to grey out disabled types.
    /// </summary>
    Task<List<DiscussionTypeEnum>> GetParentAllowedTypesForSpaceAsync(string spacePublicId, CancellationToken ct = default);
}
