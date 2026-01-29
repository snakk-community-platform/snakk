namespace Snakk.Application.UseCases;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public class SpaceUseCase(
    ISpaceRepository spaceRepository,
    IHubRepository hubRepository) : UseCaseBase
{
    private readonly ISpaceRepository _spaceRepository = spaceRepository;
    private readonly IHubRepository _hubRepository = hubRepository;

    public async Task<Result<Space>> CreateSpaceAsync(
        HubId hubId,
        string name,
        string slug,
        string? description = null)
    {
        // Validate hub exists
        var hub = await _hubRepository.GetByPublicIdAsync(hubId);
        if (hub == null)
            return Result<Space>.Failure($"Hub '{hubId}' not found");

        // Create space
        var space = Space.Create(hubId, name, slug, description);

        // Persist
        await _spaceRepository.AddAsync(space);

        return Result<Space>.Success(space);
    }

    public async Task<Result<Space>> GetSpaceAsync(SpaceId spaceId)
    {
        var space = await _spaceRepository.GetByPublicIdAsync(spaceId);
        if (space == null)
            return Result<Space>.Failure($"Space '{spaceId}' not found");

        return Result<Space>.Success(space);
    }

    public async Task<Result<Space>> GetSpaceBySlugAsync(string slug)
    {
        var space = await _spaceRepository.GetBySlugAsync(slug);
        if (space == null)
            return Result<Space>.Failure($"Space with slug '{slug}' not found");

        return Result<Space>.Success(space);
    }

    public async Task<PagedResult<Space>> GetSpacesByHubAsync(HubId hubId, int offset = 0, int pageSize = 20)
    {
        return await _spaceRepository.GetFilteredForDisplayAsync(hubId, offset, pageSize);
    }

    public async Task<Result<Space>> UpdateSpaceNameAsync(
        SpaceId spaceId,
        string newName)
    {
        var space = await _spaceRepository.GetByPublicIdAsync(spaceId);
        if (space == null)
            return Result<Space>.Failure($"Space '{spaceId}' not found");

        space.UpdateName(newName);
        await _spaceRepository.UpdateAsync(space);

        return Result<Space>.Success(space);
    }

    public async Task<Result<Space>> UpdateSpaceDescriptionAsync(
        SpaceId spaceId,
        string? newDescription)
    {
        var space = await _spaceRepository.GetByPublicIdAsync(spaceId);
        if (space == null)
            return Result<Space>.Failure($"Space '{spaceId}' not found");

        space.UpdateDescription(newDescription);
        await _spaceRepository.UpdateAsync(space);

        return Result<Space>.Success(space);
    }

    public async Task<Result<Space>> UpdateSpaceSlugAsync(
        SpaceId spaceId,
        string newSlug)
    {
        var space = await _spaceRepository.GetByPublicIdAsync(spaceId);
        if (space == null)
            return Result<Space>.Failure($"Space '{spaceId}' not found");

        space.UpdateSlug(newSlug);
        await _spaceRepository.UpdateAsync(space);

        return Result<Space>.Success(space);
    }
}
