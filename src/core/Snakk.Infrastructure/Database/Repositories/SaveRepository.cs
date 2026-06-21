namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Extensions;
using Snakk.Shared.Models;

public class SaveRepository(SnakkDbContext context, HybridCache cache) : ISaveRepository
{
    private static readonly HybridCacheEntryOptions _spaceDisplayCacheOptions = new()
    {
        Expiration = TimeSpan.FromDays(365),
        LocalCacheExpiration = TimeSpan.FromDays(365),
    };

    public async Task<bool> ToggleSaveDiscussionAsync(string userId, string discussionPublicId, CancellationToken ct = default)
    {
        var user = await context.Users.Where(u => u.PublicId == userId).Select(u => new { u.Id }).FirstOrDefaultAsync(ct);
        if (user is null) return false;

        var discussion = await context.Discussions.Where(d => d.PublicId == discussionPublicId).Select(d => new { d.Id }).FirstOrDefaultAsync(ct);
        if (discussion is null) return false;

        var existing = await context.UserSaves
            .Where(s => s.UserId == user.Id && s.DiscussionId == discussion.Id)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            context.UserSaves.Remove(existing);
            await context.SaveChangesAsync(ct);
            return false;
        }

        context.UserSaves.Add(new UserSaveDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            UserId = user.Id,
            DiscussionId = discussion.Id,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ToggleSavePostAsync(string userId, string postPublicId, CancellationToken ct = default)
    {
        var user = await context.Users.Where(u => u.PublicId == userId).Select(u => new { u.Id }).FirstOrDefaultAsync(ct);
        if (user is null) return false;

        var post = await context.Posts.Where(p => p.PublicId == postPublicId).Select(p => new { p.Id }).FirstOrDefaultAsync(ct);
        if (post is null) return false;

        var existing = await context.UserSaves
            .Where(s => s.UserId == user.Id && s.PostId == post.Id)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            context.UserSaves.Remove(existing);
            await context.SaveChangesAsync(ct);
            return false;
        }

        context.UserSaves.Add(new UserSaveDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            UserId = user.Id,
            PostId = post.Id,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<string>> GetSavedDiscussionIdsAsync(string userId, CancellationToken ct = default)
    {
        var userDbId = await ResolveUserIdAsync(userId, ct);
        if (userDbId == 0) return [];

        return await context.UserSaves
            .Where(s => s.UserId == userDbId && s.DiscussionId != null)
            .Select(s => s.Discussion!.PublicId)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetSavedPostIdsAsync(string userId, CancellationToken ct = default)
    {
        var userDbId = await ResolveUserIdAsync(userId, ct);
        if (userDbId == 0) return [];

        return await context.UserSaves
            .Where(s => s.UserId == userDbId && s.PostId != null)
            .Select(s => s.Post!.PublicId)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<Application.Repositories.RecentDiscussionDto>> GetSavedDiscussionsAsync(string userId, int offset, int pageSize, CancellationToken ct = default)
    {
        var userDbId = await ResolveUserIdAsync(userId, ct);
        if (userDbId == 0)
            return new PagedResult<Application.Repositories.RecentDiscussionDto>
            {
                Items = [],
                Offset = offset,
                PageSize = pageSize,
                HasMoreItems = false
            };

        var rawItems = await context.UserSaves
            .Where(s => s.UserId == userDbId && s.DiscussionId != null && !s.Discussion!.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(s => new
            {
                s.Id,
                SavedAt = s.CreatedAt,
                PublicId = s.Discussion!.PublicId,
                s.Discussion.Title,
                s.Discussion.Slug,
                s.Discussion.Type,
                s.Discussion.CreatedAt,
                s.Discussion.LastActivityAt,
                s.Discussion.IsPinned,
                s.Discussion.IsLocked,
                SpaceId = s.Discussion.SpaceId,
                s.Discussion.SpacePublicId,
                s.Discussion.HubPublicId,
                s.Discussion.CommunityPublicId,
                s.Discussion.CreatedByUserPublicId,
                s.Discussion.AuthorDisplayName,
                s.Discussion.AuthorAvatarFileName,
                s.Discussion.AuthorAvatarThumbnailFileName,
                s.Discussion.PostCount,
                s.Discussion.ReactionCount,
                s.Discussion.Tags,
                s.Discussion.LastPostAuthorPublicId,
                s.Discussion.LastPostAuthorDisplayName,
                s.Discussion.LastPostAuthorAvatarFileName,
                s.Discussion.LastPostAuthorAvatarThumbnailFileName,
                s.Discussion.LastPostPlainTextExcerpt,
                s.Discussion.IsAdultOnly
            })
            .ToListAsync(ct);

        var hasMoreItems = rawItems.Count > pageSize;
        var page = hasMoreItems ? rawItems.Take(pageSize).ToList() : rawItems;

        var spaceDisplay = await FetchSpaceDisplayAsync(page.Select(x => x.SpaceId), ct);

        return new PagedResult<Application.Repositories.RecentDiscussionDto>
        {
            Items = page.Select(d =>
            {
                spaceDisplay.TryGetValue(d.SpaceId, out var space);
                return new Application.Repositories.RecentDiscussionDto(
                    d.PublicId,
                    d.Title,
                    d.Slug,
                    d.Type,
                    d.CreatedAt,
                    d.LastActivityAt,
                    d.IsPinned,
                    d.IsLocked,
                    d.SpacePublicId!,
                    space?.Slug ?? "",
                    space?.Name ?? "",
                    d.HubPublicId!,
                    space?.HubSlug ?? "",
                    space?.HubName ?? "",
                    d.CommunityPublicId,
                    space?.CommunitySlug,
                    space?.CommunityName,
                    d.CreatedByUserPublicId,
                    d.AuthorDisplayName ?? "",
                    d.AuthorAvatarFileName,
                    d.AuthorAvatarThumbnailFileName,
                    d.PostCount,
                    d.ReactionCount,
                    string.IsNullOrEmpty(d.Tags)
                        ? Array.Empty<string>()
                        : d.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
                    LastReplierPublicId: d.LastPostAuthorPublicId,
                    LastReplierDisplayName: d.LastPostAuthorDisplayName,
                    LastReplierAvatarFileName: d.LastPostAuthorAvatarFileName,
                    LastReplierAvatarThumbnailFileName: d.LastPostAuthorAvatarThumbnailFileName,
                    LastPostExcerpt: d.LastPostPlainTextExcerpt,
                    IsAdult: d.IsAdultOnly,
                    SavedAt: d.SavedAt);
            }).ToList(),
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<PagedResult<SavedPostDto>> GetSavedPostsAsync(string userId, int offset, int pageSize, CancellationToken ct = default)
    {
        var userDbId = await ResolveUserIdAsync(userId, ct);
        if (userDbId == 0)
            return new PagedResult<SavedPostDto>
            {
                Items = [],
                Offset = offset,
                PageSize = pageSize,
                HasMoreItems = false
            };

        return await context.UserSaves
            .Where(s => s.UserId == userDbId && s.PostId != null && !s.Post!.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SavedPostDto(
                s.Post!.PublicId,
                // Prefer PlainTextExcerpt (markdown-stripped on save) over raw content.
                // Fallback covers legacy rows; consuming template auto-encodes.
                s.Post.PlainTextExcerpt
                    ?? (s.Post.Content.Length > 300
                        ? s.Post.Content.Substring(0, 300)
                        : s.Post.Content),
                s.Post.CreatedAt,
                s.Post.DiscussionPublicId,
                s.Post.Discussion.Title,
                s.Post.Discussion.Slug,
                s.Post.Discussion.Space.Slug,
                s.Post.Discussion.Space.Name,
                s.Post.Discussion.Space.HubSlug!,
                s.Post.Discussion.Space.HubName!,
                s.Post.Discussion.Space.CommunitySlug,
                s.Post.CreatedByUserPublicId,
                s.Post.CreatedByUser.DisplayName ?? "",
                s.Post.CreatedByUser.AvatarFileName,
                s.CreatedAt,
                s.Post.CreatedByUser.Slug))
            .ToPagedResultAsync(offset, pageSize, ct);
    }

    public async Task<(int DiscussionCount, int PostCount)> GetSaveCountsAsync(string userId, CancellationToken ct = default)
    {
        var userDbId = await ResolveUserIdAsync(userId, ct);
        if (userDbId == 0) return (0, 0);

        var discussionCount = await context.UserSaves
            .Where(s => s.UserId == userDbId && s.DiscussionId != null)
            .CountAsync(ct);

        var postCount = await context.UserSaves
            .Where(s => s.UserId == userDbId && s.PostId != null)
            .CountAsync(ct);

        return (discussionCount, postCount);
    }

    private Task<int> ResolveUserIdAsync(string userId, CancellationToken ct = default) =>
        context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

    private sealed record SpaceDisplay(
        string Slug, string Name,
        string? Description,
        string? AvatarFileName, string? AvatarThumbnailFileName,
        string? HubSlug, string? HubName,
        string? CommunitySlug, string? CommunityName);

    private async Task<Dictionary<int, SpaceDisplay>> FetchSpaceDisplayAsync(IEnumerable<int> spaceIds, CancellationToken ct = default)
    {
        var ids = spaceIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        var result = new Dictionary<int, SpaceDisplay>(ids.Count);
        foreach (var id in ids)
        {
            var display = await cache.GetOrCreateAsync<SpaceDisplay?>(
                $"space-display:{id}",
                ct2 => FetchSingleSpaceDisplayAsync(id, ct2),
                _spaceDisplayCacheOptions, cancellationToken: ct);
            if (display is not null) result[id] = display;
        }
        return result;
    }

    private async ValueTask<SpaceDisplay?> FetchSingleSpaceDisplayAsync(int spaceId, CancellationToken ct) =>
        await context.Spaces
            .Where(s => s.Id == spaceId)
            .Select(s => new SpaceDisplay(s.Slug, s.Name, s.Description, s.AvatarFileName, s.AvatarThumbnailFileName, s.HubSlug, s.HubName, s.CommunitySlug, s.CommunityName))
            .FirstOrDefaultAsync(ct);
}
