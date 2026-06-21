namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class ReactionQueryRepository(SnakkDbContext context) : IReactionQueryRepository
{
    public async Task<PagedResult<ReactedPostDto>> GetReactedPostsByUserAsync(
        string userId, int offset, int pageSize, CancellationToken ct = default)
    {
        return await QueryAsync(userId, offset, pageSize, firstPost: false, ct);
    }

    public async Task<PagedResult<ReactedPostDto>> GetReactedDiscussionsByUserAsync(
        string userId, int offset, int pageSize, CancellationToken ct = default)
    {
        return await QueryAsync(userId, offset, pageSize, firstPost: true, ct);
    }

    private async Task<PagedResult<ReactedPostDto>> QueryAsync(
        string userId, int offset, int pageSize, bool firstPost, CancellationToken ct = default)
    {
        var raw = await context.PostReactions
            .Where(r => r.User.PublicId == userId && r.Post.IsFirstPost == firstPost)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(r => new
            {
                r.Post.PublicId,
                // Prefer PlainTextExcerpt (markdown-stripped, populated by
                // PostRepositoryAdapter on every save). Fall back to a raw-content
                // substring for legacy rows where PlainTextExcerpt is still null;
                // the consuming template auto-encodes either way.
                ContentExcerpt = r.Post.PlainTextExcerpt
                    ?? (r.Post.Content.Length > 300
                        ? r.Post.Content.Substring(0, 300)
                        : r.Post.Content),
                PostCreatedAt = r.Post.CreatedAt,
                DiscussionPublicId = r.Post.DiscussionPublicId,
                DiscussionTitle = r.Post.Discussion.Title,
                DiscussionSlug = r.Post.Discussion.Slug,
                SpaceSlug = r.Post.Discussion.Space.Slug,
                SpaceName = r.Post.Discussion.Space.Name,
                HubSlug = r.Post.Discussion.Space.HubSlug,
                HubName = r.Post.Discussion.Space.HubName,
                CommunitySlug = r.Post.Discussion.Space.CommunitySlug,
                AuthorPublicId = r.Post.CreatedByUserPublicId,
                ReactedAt = r.CreatedAt,
                r.TypeId
            })
            .ToListAsync(ct);

        var hasMore = raw.Count > pageSize;
        var page = raw.Take(pageSize).ToList();

        var authorIds = page
            .Select(r => r.AuthorPublicId)
            .OfType<string>()
            .Distinct()
            .ToList();

        var authorMap = authorIds.Count == 0
            ? []
            : await context.Users
                .Where(u => authorIds.Contains(u.PublicId))
                .Select(u => new { u.PublicId, u.DisplayName, u.AvatarFileName })
                .ToDictionaryAsync(u => u.PublicId!, u => (u.DisplayName, u.AvatarFileName), ct);

        var items = page.Select(r =>
        {
            authorMap.TryGetValue(r.AuthorPublicId ?? "", out var author);
            return new ReactedPostDto(
                r.PublicId,
                r.ContentExcerpt,
                r.PostCreatedAt,
                r.DiscussionPublicId,
                r.DiscussionTitle,
                r.DiscussionSlug,
                r.SpaceSlug,
                r.SpaceName ?? "",
                r.HubSlug!,
                r.HubName ?? "",
                r.CommunitySlug!,
                r.AuthorPublicId ?? "",
                author.DisplayName ?? "",
                author.AvatarFileName,
                r.ReactedAt,
                ((ReactionTypeEnum)r.TypeId).ToString());
        }).ToList();

        return new PagedResult<ReactedPostDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMore
        };
    }
}
