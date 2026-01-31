namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class CommunityRepositoryAdapter(
    Infrastructure.Database.Repositories.ICommunityDatabaseRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.ICommunityRepository
{
    private readonly Infrastructure.Database.Repositories.ICommunityDatabaseRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<Community?> GetByPublicIdAsync(CommunityId publicId)
    {
        var entity = await _databaseRepository.GetByPublicIdAsync(publicId.Value);
        return entity?.FromPersistence();
    }

    public async Task<Community?> GetBySlugAsync(string slug)
    {
        var entity = await _databaseRepository.GetBySlugAsync(slug);
        return entity?.FromPersistence();
    }

    public async Task<Community?> GetByDomainAsync(string domain)
    {
        var entity = await _databaseRepository.GetByDomainAsync(domain);
        return entity?.FromPersistence();
    }

    public async Task<PagedResult<Community>> GetPublicListedAsync(int offset, int pageSize)
    {
        var result = await _databaseRepository.GetPublicListedAsync(offset, pageSize);
        return new PagedResult<Community>
        {
            Items = result.Items.Select(dto => Community.RehydrateForList(
                CommunityId.From(dto.PublicId),
                dto.Name,
                dto.Slug,
                dto.Description,
                Enum.Parse<CommunityVisibility>(dto.Visibility),
                dto.ExposeToPlatformFeed,
                dto.CreatedAt)).ToList(),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task<PagedResult<Community>> GetForPlatformFeedAsync(int offset, int pageSize)
    {
        var result = await _databaseRepository.GetForPlatformFeedAsync(offset, pageSize);
        return new PagedResult<Community>
        {
            Items = result.Items.Select(dto => Community.RehydrateForList(
                CommunityId.From(dto.PublicId),
                dto.Name,
                dto.Slug,
                dto.Description,
                Enum.Parse<CommunityVisibility>(dto.Visibility),
                dto.ExposeToPlatformFeed,
                dto.CreatedAt)).ToList(),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task AddAsync(Community community)
    {
        var entity = community.ToPersistence();
        await _databaseRepository.AddAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Community community)
    {
        var entity = await _context.Communities.FirstOrDefaultAsync(c => c.PublicId == community.PublicId.Value);
        if (entity == null)
            throw new InvalidOperationException($"Community with PublicId '{community.PublicId}' not found");

        entity.Name = community.Name;
        entity.Slug = community.Slug;
        entity.Description = community.Description;
        entity.VisibilityId = (int)community.Visibility.ToShared();
        entity.ExposeToPlatformFeed = community.ExposeToPlatformFeed;
        entity.LastModifiedAt = community.LastModifiedAt;

        await _databaseRepository.UpdateAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }
}
