namespace Snakk.Application.UseCases;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public class CommunityUseCase(
    ICommunityRepository communityRepository) : UseCaseBase
{
    private readonly ICommunityRepository _communityRepository = communityRepository;

    public async Task<Result<Community>> CreateCommunityAsync(
        string name,
        string slug,
        string? description = null,
        CommunityVisibility visibility = CommunityVisibility.PublicListed,
        bool exposeToPlatformFeed = true)
    {
        // Check if slug is already taken
        var existing = await _communityRepository.GetBySlugAsync(slug);
        if (existing != null)
            return Result<Community>.Failure($"Community with slug '{slug}' already exists");

        // Create community
        var community = Community.Create(name, slug, description, visibility, exposeToPlatformFeed);

        // Persist
        await _communityRepository.AddAsync(community);

        return Result<Community>.Success(community);
    }

    public async Task<Result<Community>> GetCommunityAsync(CommunityId communityId)
    {
        var community = await _communityRepository.GetByPublicIdAsync(communityId);
        if (community == null)
            return Result<Community>.Failure($"Community '{communityId}' not found");

        return Result<Community>.Success(community);
    }

    public async Task<Result<Community>> GetCommunityBySlugAsync(string slug)
    {
        var community = await _communityRepository.GetBySlugAsync(slug);
        if (community == null)
            return Result<Community>.Failure($"Community with slug '{slug}' not found");

        return Result<Community>.Success(community);
    }

    public async Task<Result<Community>> GetCommunityByDomainAsync(string domain)
    {
        var community = await _communityRepository.GetByDomainAsync(domain);
        if (community == null)
            return Result<Community>.Failure($"Community with domain '{domain}' not found");

        return Result<Community>.Success(community);
    }

    public async Task<PagedResult<Community>> GetPublicCommunitiesAsync(int offset = 0, int pageSize = 20)
    {
        return await _communityRepository.GetPublicListedAsync(offset, pageSize);
    }

    public async Task<PagedResult<Community>> GetCommunitiesForPlatformFeedAsync(int offset = 0, int pageSize = 20)
    {
        return await _communityRepository.GetForPlatformFeedAsync(offset, pageSize);
    }

    public async Task<Result<Community>> UpdateCommunityNameAsync(
        CommunityId communityId,
        string newName)
    {
        var community = await _communityRepository.GetByPublicIdAsync(communityId);
        if (community == null)
            return Result<Community>.Failure($"Community '{communityId}' not found");

        community.UpdateName(newName);
        await _communityRepository.UpdateAsync(community);

        return Result<Community>.Success(community);
    }

    public async Task<Result<Community>> UpdateCommunityDescriptionAsync(
        CommunityId communityId,
        string? newDescription)
    {
        var community = await _communityRepository.GetByPublicIdAsync(communityId);
        if (community == null)
            return Result<Community>.Failure($"Community '{communityId}' not found");

        community.UpdateDescription(newDescription);
        await _communityRepository.UpdateAsync(community);

        return Result<Community>.Success(community);
    }

    public async Task<Result<Community>> UpdateCommunityVisibilityAsync(
        CommunityId communityId,
        CommunityVisibility visibility)
    {
        var community = await _communityRepository.GetByPublicIdAsync(communityId);
        if (community == null)
            return Result<Community>.Failure($"Community '{communityId}' not found");

        community.UpdateVisibility(visibility);
        await _communityRepository.UpdateAsync(community);

        return Result<Community>.Success(community);
    }

    public async Task<Result<Community>> SetExposeToPlatformFeedAsync(
        CommunityId communityId,
        bool expose)
    {
        var community = await _communityRepository.GetByPublicIdAsync(communityId);
        if (community == null)
            return Result<Community>.Failure($"Community '{communityId}' not found");

        community.SetExposeToPlatformFeed(expose);
        await _communityRepository.UpdateAsync(community);

        return Result<Community>.Success(community);
    }
}
