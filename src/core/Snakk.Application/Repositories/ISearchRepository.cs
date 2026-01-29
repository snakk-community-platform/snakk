namespace Snakk.Application.Repositories;

using Snakk.Shared.Models;

public interface ISearchRepository
{
    Task<PagedResult<DiscussionSearchResultDto>> SearchDiscussionsAsync(
        string query,
        string? authorPublicId = null,
        string? spacePublicId = null,
        string? hubPublicId = null,
        int offset = 0,
        int pageSize = 20);

    Task<PagedResult<PostSearchResultDto>> SearchPostsAsync(
        string query,
        string? authorPublicId = null,
        string? discussionPublicId = null,
        string? spacePublicId = null,
        int offset = 0,
        int pageSize = 20);

    Task<int> GetDiscussionCountByAuthorAsync(string authorPublicId);
    Task<int> GetPostCountByAuthorAsync(string authorPublicId);
}

public record DiscussionSearchResultDto(
    string PublicId,
    string Title,
    string Slug,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    string SpacePublicId,
    string SpaceName,
    string SpaceSlug,
    string HubSlug,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    int PostCount,
    int ViewCount);

public record PostSearchResultDto(
    string PublicId,
    string Content,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    string DiscussionPublicId,
    string DiscussionTitle,
    string DiscussionSlug,
    string SpaceSlug,
    string HubSlug,
    DateTime CreatedAt);
