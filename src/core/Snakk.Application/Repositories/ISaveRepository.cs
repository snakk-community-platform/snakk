namespace Snakk.Application.Repositories;

using Snakk.Shared.Models;

public interface ISaveRepository
{
    Task<bool> ToggleSaveDiscussionAsync(string userId, string discussionPublicId);
    Task<bool> ToggleSavePostAsync(string userId, string postPublicId);
    Task<List<string>> GetSavedDiscussionIdsAsync(string userId);
    Task<List<string>> GetSavedPostIdsAsync(string userId);
    Task<PagedResult<RecentDiscussionDto>> GetSavedDiscussionsAsync(string userId, int offset, int pageSize);
    Task<PagedResult<SavedPostDto>> GetSavedPostsAsync(string userId, int offset, int pageSize);
    Task<(int DiscussionCount, int PostCount)> GetSaveCountsAsync(string userId);
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
