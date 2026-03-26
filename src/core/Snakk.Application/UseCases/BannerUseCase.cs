namespace Snakk.Application.UseCases;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;
using Snakk.Application.Services;

public class BannerUseCase(
    IBannerRepository announcementRepository,
    IUserRepository userRepository,
    IMarkupParser markupParser) : UseCaseBase
{
    public async Task<Result<Banner>> CreateAsync(
        BannerScopeEnum scope,
        string scopeEntityId,
        UserId createdByUserId,
        string title,
        string content,
        BannerTypeEnum type = BannerTypeEnum.Info,
        DateTime? visibleFrom = null,
        DateTime? visibleUntil = null,
        bool isDismissible = true,
        int sortOrder = 0)
    {
        var user = await userRepository.GetByPublicIdAsync(createdByUserId);

        if (user is null)
            return Result<Banner>.Failure($"User '{createdByUserId}' not found");

        try
        {
            var renderedContent = markupParser.ToHtml(content);
            var banner = Banner.Create(
                scope,
                scopeEntityId,
                createdByUserId,
                title,
                content,
                renderedContent,
                type,
                visibleFrom,
                visibleUntil,
                isDismissible,
                sortOrder);

            await announcementRepository.AddAsync(banner);

            return Result<Banner>.Success(banner);
        }
        catch (ArgumentException ex)
        {
            return Result<Banner>.Failure(ex.Message);
        }
    }

    public async Task<Result<Banner>> UpdateAsync(
        BannerId publicId,
        string title,
        string content,
        BannerTypeEnum type,
        DateTime? visibleFrom,
        DateTime? visibleUntil,
        bool isDismissible,
        int sortOrder)
    {
        var banner = await announcementRepository.GetByPublicIdAsync(publicId);

        if (banner is null)
            return Result<Banner>.Failure($"Banner '{publicId}' not found");

        try
        {
            var renderedContent = markupParser.ToHtml(content);
            banner.Update(title, content, renderedContent, type, visibleFrom, visibleUntil, isDismissible, sortOrder);
            await announcementRepository.UpdateAsync(banner);

            return Result<Banner>.Success(banner);
        }
        catch (ArgumentException ex)
        {
            return Result<Banner>.Failure(ex.Message);
        }
    }

    public async Task<Result> DeleteAsync(BannerId publicId)
    {
        var banner = await announcementRepository.GetByPublicIdAsync(publicId);

        if (banner is null)
            return Result.Failure($"Banner '{publicId}' not found");

        try
        {
            await announcementRepository.DeleteAsync(publicId);
            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result<Banner>> GetByIdAsync(BannerId publicId)
    {
        var banner = await announcementRepository.GetByPublicIdAsync(publicId);

        if (banner is null)
            return Result<Banner>.Failure($"Banner '{publicId}' not found");

        return Result<Banner>.Success(banner);
    }

    public async Task<IEnumerable<Banner>> GetByScopeAsync(
        BannerScopeEnum scope,
        string scopeEntityId) =>
        await announcementRepository.GetByScopeAsync(scope, scopeEntityId);

    public async Task<IEnumerable<Banner>> GetActiveForCommunityAsync(CommunityId communityId) =>
        await announcementRepository.GetActiveForCommunityAsync(communityId);

    public async Task<IEnumerable<Banner>> GetActiveForHubAsync(HubId hubId) =>
        await announcementRepository.GetActiveForHubAsync(hubId);

    public async Task<IEnumerable<Banner>> GetActiveForSpaceAsync(SpaceId spaceId) =>
        await announcementRepository.GetActiveForSpaceAsync(spaceId);
}
