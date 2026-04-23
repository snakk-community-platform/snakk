namespace Snakk.Application.Repositories;

using Snakk.Shared.Models;

public interface IReactionQueryRepository
{
    Task<PagedResult<ReactedPostDto>> GetReactedPostsByUserAsync(string userId, int offset, int pageSize);
    Task<PagedResult<ReactedPostDto>> GetReactedDiscussionsByUserAsync(string userId, int offset, int pageSize);
}

public record ReactedPostDto(
    string PublicId,
    string ContentExcerpt,
    DateTime PostCreatedAt,
    string DiscussionPublicId,
    string DiscussionTitle,
    string DiscussionSlug,
    string SpaceSlug,
    string HubSlug,
    string CommunitySlug,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    DateTime ReactedAt,
    string ReactionType);
