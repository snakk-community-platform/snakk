namespace Snakk.Application.Repositories;

using Snakk.Shared.Models;

public interface ISaveRepository
{
    Task<bool> ToggleSaveDiscussionAsync(string userId, string discussionPublicId, CancellationToken ct = default);
    Task<bool> ToggleSavePostAsync(string userId, string postPublicId, CancellationToken ct = default);
    Task<List<string>> GetSavedDiscussionIdsAsync(string userId, CancellationToken ct = default);
    Task<List<string>> GetSavedPostIdsAsync(string userId, CancellationToken ct = default);
    Task<PagedResult<RecentDiscussionDto>> GetSavedDiscussionsAsync(string userId, int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<SavedPostDto>> GetSavedPostsAsync(string userId, int offset, int pageSize, CancellationToken ct = default);
    Task<(int DiscussionCount, int PostCount)> GetSaveCountsAsync(string userId, CancellationToken ct = default);
}

public record SavedPostDto(
    string PublicId,
    string ContentExcerpt,
    DateTime CreatedAt,
    string DiscussionPublicId,
    string DiscussionTitle,
    string DiscussionSlug,
    string SpaceSlug,
    string SpaceName,
    string HubSlug,
    string HubName,
    string CommunitySlug,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    DateTime SavedAt);
