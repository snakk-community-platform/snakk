namespace Snakk.Application.Repositories;

using Snakk.Shared.Models;

public interface IReactionQueryRepository
{
    Task<PagedResult<ReactedPostDto>> GetReactedPostsByUserAsync(string userId, int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<ReactedPostDto>> GetReactedDiscussionsByUserAsync(string userId, int offset, int pageSize, CancellationToken ct = default);
}

public record ReactedPostDto(
    string PublicId,
    string ContentExcerpt,
    DateTime PostCreatedAt,
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
    DateTime ReactedAt,
    string ReactionType);
