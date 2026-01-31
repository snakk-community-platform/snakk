namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Models;

public class DiscussionRepositoryAdapter(
    Infrastructure.Database.Repositories.IDiscussionRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.IDiscussionRepository
{
    private readonly Infrastructure.Database.Repositories.IDiscussionRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<Discussion?> GetByIdAsync(int id)
    {
        var entity = await _databaseRepository.GetByIdAsync(id);
        return entity?.FromPersistence();
    }

    public async Task<Discussion?> GetByPublicIdAsync(DiscussionId publicId)
    {
        var entity = await _databaseRepository.GetForUpdateAsync(publicId.Value);
        return entity?.FromPersistence();
    }

    public async Task<Discussion?> GetBySlugAsync(string slug)
    {
        var entity = await _databaseRepository.GetBySlugAsync(slug);
        return entity?.FromPersistence();
    }

    public async Task<IEnumerable<Discussion>> GetBySpaceIdAsync(SpaceId spaceId)
    {
        // Get internal ID from PublicId
        var space = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == spaceId.Value);
        if (space == null)
            return [];

        var entities = await _databaseRepository.GetBySpaceIdAsync(space.Id);
        return entities.Select(e => e.FromPersistence());
    }

    public async Task<PagedResult<Discussion>> GetBySpaceIdAsync(SpaceId spaceId, int offset, int pageSize)
    {
        return await GetPagedBySpaceIdAsync(spaceId, offset, pageSize);
    }

    public async Task<PagedResult<Discussion>> GetPagedBySpaceIdAsync(
        SpaceId spaceId,
        int offset,
        int pageSize)
    {
        // Get internal ID from PublicId
        var space = await _context.Spaces
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PublicId == spaceId.Value);
        if (space == null)
            return new PagedResult<Discussion>
            {
                Items = [],
                Offset = offset,
                PageSize = pageSize,
                HasMoreItems = false
            };

        // Use the database repository's DTO-based method
        var result = await _databaseRepository.GetPagedBySpaceIdAsync(space.Id, offset, pageSize);

        return new PagedResult<Discussion>
        {
            Items = result.Items.Select(dto => Discussion.RehydrateForList(
                DiscussionId.From(dto.PublicId),
                SpaceId.From(spaceId.Value),
                UserId.From(dto.CreatedByUserPublicId),
                dto.Title,
                dto.Slug,
                dto.CreatedAt,
                dto.LastActivityAt,
                dto.IsPinned,
                dto.IsLocked)).ToList(),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task<IEnumerable<Discussion>> GetRecentAsync(int count = 10)
    {
        var entities = await _databaseRepository.GetRecentAsync(count);
        return entities.Select(e => e.FromPersistence());
    }

    public async Task AddAsync(Discussion discussion)
    {
        // Convert to database entity
        var entity = discussion.ToPersistence();

        // Resolve foreign keys from PublicIds
        var space = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == discussion.SpaceId.Value);
        if (space == null)
            throw new InvalidOperationException($"Space with PublicId '{discussion.SpaceId}' not found");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == discussion.CreatedByUserId.Value);
        if (user == null)
            throw new InvalidOperationException($"User with PublicId '{discussion.CreatedByUserId}' not found");

        entity.SpaceId = space.Id;
        entity.CreatedByUserId = user.Id;

        await _databaseRepository.AddAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Discussion discussion)
    {
        // Fetch existing entity
        var entity = await _context.Discussions.FirstOrDefaultAsync(d => d.PublicId == discussion.PublicId.Value);
        if (entity == null)
            throw new InvalidOperationException($"Discussion with PublicId '{discussion.PublicId}' not found");

        // Update properties
        entity.Title = discussion.Title;
        entity.Slug = discussion.Slug;
        entity.LastModifiedAt = discussion.LastModifiedAt;
        entity.LastActivityAt = discussion.LastActivityAt;
        entity.IsPinned = discussion.IsPinned;
        entity.IsLocked = discussion.IsLocked;

        await _databaseRepository.UpdateAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task<List<Domain.Repositories.TopActiveDiscussion>> GetTopActiveDiscussionsSinceAsync(
        DateTime since,
        HubId? hubId,
        SpaceId? spaceId,
        CommunityId? communityId,
        int limit)
    {
        var postsQuery = _context.Posts
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.CreatedAt >= since);

        // Apply filters based on hierarchy
        if (communityId != null)
        {
            postsQuery = postsQuery.Where(p =>
                p.Discussion.Space.Hub.CommunityId == _context.Communities
                    .Where(c => c.PublicId == communityId.Value)
                    .Select(c => c.Id)
                    .FirstOrDefault());
        }

        if (hubId != null)
        {
            postsQuery = postsQuery.Where(p =>
                p.Discussion.Space.HubId == _context.Hubs
                    .Where(h => h.PublicId == hubId.Value)
                    .Select(h => h.Id)
                    .FirstOrDefault());
        }

        if (spaceId != null)
        {
            postsQuery = postsQuery.Where(p =>
                p.Discussion.SpaceId == _context.Spaces
                    .Where(s => s.PublicId == spaceId.Value)
                    .Select(s => s.Id)
                    .FirstOrDefault());
        }

        var topDiscussions = await postsQuery
            .GroupBy(p => new
            {
                DiscussionPublicId = p.Discussion.PublicId,
                p.Discussion.Title,
                p.Discussion.Slug,
                AuthorPublicId = p.Discussion.CreatedByUser != null ? p.Discussion.CreatedByUser.PublicId : "",
                AuthorDisplayName = p.Discussion.CreatedByUser != null ? p.Discussion.CreatedByUser.DisplayName : "",
                SpacePublicId = p.Discussion.Space.PublicId,
                SpaceSlug = p.Discussion.Space.Slug,
                SpaceName = p.Discussion.Space.Name,
                HubPublicId = p.Discussion.Space.Hub.PublicId,
                HubSlug = p.Discussion.Space.Hub.Slug,
                HubName = p.Discussion.Space.Hub.Name
            })
            .Select(g => new
            {
                PublicId = g.Key.DiscussionPublicId,
                g.Key.Title,
                g.Key.Slug,
                PostCount = g.Count(),
                g.Key.SpacePublicId,
                g.Key.SpaceSlug,
                g.Key.SpaceName,
                g.Key.HubPublicId,
                g.Key.HubSlug,
                g.Key.HubName,
                g.Key.AuthorPublicId,
                g.Key.AuthorDisplayName
            })
            .OrderByDescending(x => x.PostCount)
            .Take(limit)
            .ToListAsync();

        return topDiscussions
            .Select(d => new Domain.Repositories.TopActiveDiscussion(
                DiscussionId.From(d.PublicId),
                d.Title,
                d.Slug,
                d.PostCount,
                d.SpacePublicId,
                d.SpaceSlug,
                d.SpaceName,
                d.HubPublicId,
                d.HubSlug,
                d.HubName,
                d.AuthorPublicId,
                d.AuthorDisplayName))
            .ToList();
    }

    public async Task<IEnumerable<(DateTime Date, int Count)>> GetActivityByDateAsync(UserId userId, DateTime startDate)
    {
        // Get the internal user ID
        var userDbId = await _context.Users
            .Where(u => u.PublicId == userId.Value)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (userDbId == 0)
            return [];

        var activity = await _context.Discussions
            .AsNoTracking()
            .Where(d => d.CreatedByUserId == userDbId && d.CreatedAt >= startDate)
            .GroupBy(d => d.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        return activity.Select(a => (a.Date, a.Count));
    }
}
