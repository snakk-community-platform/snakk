namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public class SaveRepository(SnakkDbContext context) : ISaveRepository
{
    public async Task<bool> ToggleSaveDiscussionAsync(string userId, string discussionPublicId)
    {
        var user = await context.Users.Where(u => u.PublicId == userId).Select(u => new { u.Id }).FirstOrDefaultAsync();
        if (user is null) return false;

        var discussion = await context.Discussions.Where(d => d.PublicId == discussionPublicId).Select(d => new { d.Id }).FirstOrDefaultAsync();
        if (discussion is null) return false;

        var existing = await context.UserSaves
            .Where(s => s.UserId == user.Id && s.DiscussionId == discussion.Id)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            context.UserSaves.Remove(existing);
            await context.SaveChangesAsync();
            return false;
        }

        context.UserSaves.Add(new UserSaveDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            UserId = user.Id,
            DiscussionId = discussion.Id,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleSavePostAsync(string userId, string postPublicId)
    {
        var user = await context.Users.Where(u => u.PublicId == userId).Select(u => new { u.Id }).FirstOrDefaultAsync();
        if (user is null) return false;

        var post = await context.Posts.Where(p => p.PublicId == postPublicId).Select(p => new { p.Id }).FirstOrDefaultAsync();
        if (post is null) return false;

        var existing = await context.UserSaves
            .Where(s => s.UserId == user.Id && s.PostId == post.Id)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            context.UserSaves.Remove(existing);
            await context.SaveChangesAsync();
            return false;
        }

        context.UserSaves.Add(new UserSaveDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            UserId = user.Id,
            PostId = post.Id,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<List<string>> GetSavedDiscussionIdsAsync(string userId)
    {
        return await context.UserSaves
            .Where(s => s.User.PublicId == userId && s.DiscussionId != null)
            .Select(s => s.Discussion!.PublicId)
            .ToListAsync();
    }

    public async Task<List<string>> GetSavedPostIdsAsync(string userId)
    {
        return await context.UserSaves
            .Where(s => s.User.PublicId == userId && s.PostId != null)
            .Select(s => s.Post!.PublicId)
            .ToListAsync();
    }

    public async Task<PagedResult<Application.Repositories.RecentDiscussionDto>> GetSavedDiscussionsAsync(string userId, int offset, int pageSize)
    {
        var items = await context.UserSaves
            .Where(s => s.User.PublicId == userId && s.DiscussionId != null && !s.Discussion!.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(s => new
            {
                s.Id,
                Dto = new Application.Repositories.RecentDiscussionDto(
                    s.Discussion!.PublicId,
                    s.Discussion.Title,
                    s.Discussion.Slug,
                    s.Discussion.Type,
                    s.Discussion.CreatedAt,
                    s.Discussion.LastActivityAt,
                    s.Discussion.IsPinned,
                    s.Discussion.IsLocked,
                    s.Discussion.Space.PublicId,
                    s.Discussion.Space.Slug,
                    s.Discussion.Space.Name,
                    s.Discussion.Space.Hub.PublicId,
                    s.Discussion.Space.Hub.Slug,
                    s.Discussion.Space.Hub.Name,
                    s.Discussion.Space.Hub.Community.PublicId,
                    s.Discussion.Space.Hub.Community.Slug,
                    s.Discussion.Space.Hub.Community.Name,
                    s.Discussion.CreatedByUser.PublicId,
                    s.Discussion.CreatedByUser.DisplayName ?? "",
                    s.Discussion.CreatedByUser.AvatarFileName,
                    s.Discussion.CreatedByUser.AvatarThumbnailFileName,
                    s.Discussion.PostCount,
                    s.Discussion.ReactionCount,
                    string.IsNullOrEmpty(s.Discussion.Tags)
                        ? Array.Empty<string>()
                        : s.Discussion.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries))
            })
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

        return new PagedResult<Application.Repositories.RecentDiscussionDto>
        {
            Items = resultItems.Select(x => x.Dto).ToList(),
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<PagedResult<SavedPostDto>> GetSavedPostsAsync(string userId, int offset, int pageSize)
    {
        var items = await context.UserSaves
            .Where(s => s.User.PublicId == userId && s.PostId != null && !s.Post!.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(s => new SavedPostDto(
                s.Post!.PublicId,
                s.Post.RenderedContent != null
                    ? s.Post.RenderedContent.Length > 300
                        ? s.Post.RenderedContent.Substring(0, 300)
                        : s.Post.RenderedContent
                    : "",
                s.Post.CreatedAt,
                s.Post.Discussion.PublicId,
                s.Post.Discussion.Title,
                s.Post.Discussion.Slug,
                s.Post.Discussion.Space.Slug,
                s.Post.Discussion.Space.Hub.Slug,
                s.Post.Discussion.Space.Hub.Community.Slug,
                s.Post.CreatedByUser.PublicId,
                s.Post.CreatedByUser.DisplayName ?? "",
                s.Post.CreatedByUser.AvatarFileName,
                s.CreatedAt))
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

        return new PagedResult<SavedPostDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<(int DiscussionCount, int PostCount)> GetSaveCountsAsync(string userId)
    {
        var discussionCount = await context.UserSaves
            .Where(s => s.User.PublicId == userId && s.DiscussionId != null)
            .CountAsync();

        var postCount = await context.UserSaves
            .Where(s => s.User.PublicId == userId && s.PostId != null)
            .CountAsync();

        return (discussionCount, postCount);
    }
}
