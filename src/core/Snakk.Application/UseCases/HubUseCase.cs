namespace Snakk.Application.UseCases;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public class HubUseCase(
    IHubRepository hubRepository,
    ICommunityRepository communityRepository) : UseCaseBase
{
    private readonly IHubRepository _hubRepository = hubRepository;
    private readonly ICommunityRepository _communityRepository = communityRepository;

    public async Task<Result<Hub>> CreateHubAsync(
        CommunityId communityId,
        string name,
        string slug,
        string? description = null)
    {
        // Verify community exists
        var community = await _communityRepository.GetByPublicIdAsync(communityId);
        if (community == null)
            return Result<Hub>.Failure($"Community '{communityId}' not found");

        // Create hub
        var hub = Hub.Create(communityId, name, slug, description);

        // Persist
        await _hubRepository.AddAsync(hub);

        return Result<Hub>.Success(hub);
    }

    public async Task<Result<Hub>> GetHubAsync(HubId hubId)
    {
        var hub = await _hubRepository.GetByPublicIdAsync(hubId);
        if (hub == null)
            return Result<Hub>.Failure($"Hub '{hubId}' not found");

        return Result<Hub>.Success(hub);
    }

    public async Task<Result<Hub>> GetHubBySlugAsync(string slug)
    {
        var hub = await _hubRepository.GetBySlugAsync(slug);
        if (hub == null)
            return Result<Hub>.Failure($"Hub with slug '{slug}' not found");

        return Result<Hub>.Success(hub);
    }

    public async Task<PagedResult<Hub>> GetAllHubsAsync(int offset = 0, int pageSize = 20)
    {
        return await _hubRepository.GetFilteredForDisplayAsync(offset, pageSize);
    }

    public async Task<PagedResult<Hub>> GetHubsByCommunityAsync(CommunityId communityId, int offset = 0, int pageSize = 20)
    {
        return await _hubRepository.GetByCommunityAsync(communityId, offset, pageSize);
    }

    public async Task<Result<Hub>> UpdateHubNameAsync(
        HubId hubId,
        string newName)
    {
        var hub = await _hubRepository.GetByPublicIdAsync(hubId);
        if (hub == null)
            return Result<Hub>.Failure($"Hub '{hubId}' not found");

        hub.UpdateName(newName);
        await _hubRepository.UpdateAsync(hub);

        return Result<Hub>.Success(hub);
    }

    public async Task<Result<Hub>> UpdateHubDescriptionAsync(
        HubId hubId,
        string? newDescription)
    {
        var hub = await _hubRepository.GetByPublicIdAsync(hubId);
        if (hub == null)
            return Result<Hub>.Failure($"Hub '{hubId}' not found");

        hub.UpdateDescription(newDescription);
        await _hubRepository.UpdateAsync(hub);

        return Result<Hub>.Success(hub);
    }

    public async Task<Result<Hub>> UpdateHubSlugAsync(
        HubId hubId,
        string newSlug)
    {
        var hub = await _hubRepository.GetByPublicIdAsync(hubId);
        if (hub == null)
            return Result<Hub>.Failure($"Hub '{hubId}' not found");

        hub.UpdateSlug(newSlug);
        await _hubRepository.UpdateAsync(hub);

        return Result<Hub>.Success(hub);
    }
}
