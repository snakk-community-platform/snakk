namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class ReactionQueryRepository(SnakkDbContext context) : IReactionQueryRepository
{
    public async Task<PagedResult<ReactedPostDto>> GetReactedPostsByUserAsync(
        string userId, int offset, int pageSize)
    {
        var raw = await context.PostReactions
            .Where(r => r.User.PublicId == userId && !r.Post.IsDeleted && !r.Post.IsFirstPost)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(r => new
            {
                r.Post.PublicId,
                ContentExcerpt = r.Post.PlainTextExcerpt ?? "",
                PostCreatedAt = r.Post.CreatedAt,
                DiscussionPublicId = r.Post.Discussion.PublicId,
                DiscussionTitle = r.Post.Discussion.Title,
                DiscussionSlug = r.Post.Discussion.Slug,
                SpaceSlug = r.Post.Discussion.Space.Slug,
                HubSlug = r.Post.Discussion.Space.Hub.Slug,
                CommunitySlug = r.Post.Discussion.Space.Hub.Community.Slug,
                AuthorPublicId = r.Post.CreatedByUser.PublicId,
                AuthorDisplayName = r.Post.CreatedByUser.DisplayName ?? "",
                AuthorAvatarFileName = r.Post.CreatedByUser.AvatarFileName,
                ReactedAt = r.CreatedAt,
                r.TypeId
            })
            .ToListAsync();

        var hasMore = raw.Count > pageSize;
        var items = raw
            .Take(pageSize)
            .Select(r => new ReactedPostDto(
                r.PublicId,
                r.ContentExcerpt,
                r.PostCreatedAt,
                r.DiscussionPublicId,
                r.DiscussionTitle,
                r.DiscussionSlug,
                r.SpaceSlug,
                r.HubSlug,
                r.CommunitySlug,
                r.AuthorPublicId,
                r.AuthorDisplayName,
                r.AuthorAvatarFileName,
                r.ReactedAt,
                ((ReactionTypeEnum)r.TypeId).ToString()))
            .ToList();

        return new PagedResult<ReactedPostDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMore
        };
    }

    public async Task<PagedResult<ReactedPostDto>> GetReactedDiscussionsByUserAsync(
        string userId, int offset, int pageSize)
    {
        var raw = await context.PostReactions
            .Where(r => r.User.PublicId == userId && !r.Post.IsDeleted && r.Post.IsFirstPost)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(r => new
            {
                r.Post.PublicId,
                ContentExcerpt = r.Post.PlainTextExcerpt ?? "",
                PostCreatedAt = r.Post.CreatedAt,
                DiscussionPublicId = r.Post.Discussion.PublicId,
                DiscussionTitle = r.Post.Discussion.Title,
                DiscussionSlug = r.Post.Discussion.Slug,
                SpaceSlug = r.Post.Discussion.Space.Slug,
                HubSlug = r.Post.Discussion.Space.Hub.Slug,
                CommunitySlug = r.Post.Discussion.Space.Hub.Community.Slug,
                AuthorPublicId = r.Post.CreatedByUser.PublicId,
                AuthorDisplayName = r.Post.CreatedByUser.DisplayName ?? "",
                AuthorAvatarFileName = r.Post.CreatedByUser.AvatarFileName,
                ReactedAt = r.CreatedAt,
                r.TypeId
            })
            .ToListAsync();

        var hasMore = raw.Count > pageSize;
        var items = raw
            .Take(pageSize)
            .Select(r => new ReactedPostDto(
                r.PublicId,
                r.ContentExcerpt,
                r.PostCreatedAt,
                r.DiscussionPublicId,
                r.DiscussionTitle,
                r.DiscussionSlug,
                r.SpaceSlug,
                r.HubSlug,
                r.CommunitySlug,
                r.AuthorPublicId,
                r.AuthorDisplayName,
                r.AuthorAvatarFileName,
                r.ReactedAt,
                ((ReactionTypeEnum)r.TypeId).ToString()))
            .ToList();

        return new PagedResult<ReactedPostDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMore
        };
    }
}
