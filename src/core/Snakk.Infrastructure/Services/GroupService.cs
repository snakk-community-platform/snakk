using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Helpers;

namespace Snakk.Infrastructure.Services;

public class GroupService(
    SnakkDbContext context,
    ILogger<GroupService> logger,
    IUserGrantsCacheService grantsCache) : IGroupService
{
    public async Task<GroupListDto> GetGroupsAsync(
        string communityId,
        CancellationToken ct = default)
    {
        var groups = await context.Groups
            .Where(g => g.Community.PublicId == communityId)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Name)
            .Select(g => new GroupDto
            {
                PublicId = g.PublicId,
                Name = g.Name,
                Slug = g.Slug,
                Description = g.Description,
                IsPublic = g.IsPublic,
                SortOrder = g.SortOrder,
                MemberCount = g.Members.Count(),
                CreatedAt = g.CreatedAt
            })
            .ToListAsync(ct);

        return new GroupListDto { Groups = groups };
    }

    public async Task<GroupDto?> GetGroupAsync(
        string communityId,
        string groupId,
        CancellationToken ct = default) =>
        await context.Groups
            .Where(g => g.Community.PublicId == communityId && g.PublicId == groupId)
            .Select(g => new GroupDto
            {
                PublicId = g.PublicId,
                Name = g.Name,
                Slug = g.Slug,
                Description = g.Description,
                IsPublic = g.IsPublic,
                SortOrder = g.SortOrder,
                MemberCount = g.Members.Count(),
                CreatedAt = g.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

    public async Task<GroupDto> CreateGroupAsync(
        string communityId,
        CreateGroupRequest request,
        string actingUserPublicId,
        CancellationToken ct = default)
    {
        var community = await context.Communities
            .Where(c => c.PublicId == communityId)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Community {communityId} not found");

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? SlugHelper.ToSlug(request.Name)
            : request.Slug.Trim().ToLowerInvariant();

        var entity = new GroupDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            CommunityId = community.Id,
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            IsPublic = request.IsPublic,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow
        };

        context.Groups.Add(entity);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Created group {GroupId} in community {CommunityId}", entity.PublicId, communityId);

        return new GroupDto
        {
            PublicId = entity.PublicId,
            Name = entity.Name,
            Slug = entity.Slug,
            Description = entity.Description,
            IsPublic = entity.IsPublic,
            SortOrder = entity.SortOrder,
            MemberCount = 0,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<GroupDto?> UpdateGroupAsync(
        string communityId,
        string groupId,
        UpdateGroupRequest request,
        CancellationToken ct = default)
    {
        var entity = await context.Groups
            .AsTracking()
            .Where(g => g.Community.PublicId == communityId && g.PublicId == groupId)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return null;

        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.IsPublic = request.IsPublic;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);

        return await GetGroupAsync(communityId, groupId, ct);
    }

    public async Task<bool> DeleteGroupAsync(
        string communityId,
        string groupId,
        CancellationToken ct = default)
    {
        var entity = await context.Groups
            .AsTracking()
            .Where(g => g.Community.PublicId == communityId && g.PublicId == groupId)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return false;

        context.Groups.Remove(entity);
        await context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<GroupMemberListDto> GetMembersAsync(
        string communityId,
        string groupId,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = context.GroupMembers
            .Where(m => m.Group.Community.PublicId == communityId && m.Group.PublicId == groupId);

        var total = await query.CountAsync(ct);

        var members = await query
            .OrderBy(m => m.User.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new GroupMemberDto
            {
                UserPublicId = m.User.PublicId,
                DisplayName = m.User.DisplayName,
                AddedAt = m.AddedAt
            })
            .ToListAsync(ct);

        return new GroupMemberListDto
        {
            Members = members,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<bool> AddMemberAsync(
        string communityId,
        string groupId,
        AddGroupMemberRequest request,
        string actingUserPublicId,
        CancellationToken ct = default)
    {
        var group = await context.Groups
            .Where(g => g.Community.PublicId == communityId && g.PublicId == groupId)
            .Select(g => new { g.Id })
            .FirstOrDefaultAsync(ct);

        if (group is null)
            return false;

        var user = await context.Users
            .Where(u => u.PublicId == request.UserPublicId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return false;

        var actingUser = await context.Users
            .Where(u => u.PublicId == actingUserPublicId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);

        if (actingUser is null)
            return false;

        var exists = await context.GroupMembers
            .AnyAsync(m => m.GroupId == group.Id && m.UserId == user.Id, ct);

        if (exists)
            return true;

        context.GroupMembers.Add(new GroupMemberDatabaseEntity
        {
            GroupId = group.Id,
            UserId = user.Id,
            AddedByUserId = actingUser.Id,
            AddedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(ct);
        grantsCache.Invalidate(request.UserPublicId);

        return true;
    }

    public async Task<bool> RemoveMemberAsync(
        string communityId,
        string groupId,
        string userPublicId,
        CancellationToken ct = default)
    {
        var member = await context.GroupMembers
            .AsTracking()
            .Where(m =>
                m.Group.Community.PublicId == communityId
                && m.Group.PublicId == groupId
                && m.User.PublicId == userPublicId)
            .FirstOrDefaultAsync(ct);

        if (member is null)
            return false;

        context.GroupMembers.Remove(member);
        await context.SaveChangesAsync(ct);
        grantsCache.Invalidate(userPublicId);

        return true;
    }

}
