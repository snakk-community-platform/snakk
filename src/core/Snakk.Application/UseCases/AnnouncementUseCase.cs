namespace Snakk.Application.UseCases;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;
using Snakk.Application.Services;

public class AnnouncementUseCase(
    IAnnouncementRepository announcementRepository,
    IUserRepository userRepository,
    IMarkupParser markupParser) : UseCaseBase
{
    public async Task<Result<Announcement>> CreateAsync(
        AnnouncementScopeEnum scope,
        string scopeEntityId,
        UserId createdByUserId,
        string title,
        string content,
        AnnouncementTypeEnum type = AnnouncementTypeEnum.Info,
        DateTime? visibleFrom = null,
        DateTime? visibleUntil = null,
        bool isDismissible = true,
        int sortOrder = 0)
    {
        var user = await userRepository.GetByPublicIdAsync(createdByUserId);

        if (user is null)
            return Result<Announcement>.Failure($"User '{createdByUserId}' not found");

        try
        {
            var renderedContent = markupParser.ToHtml(content);
            var announcement = Announcement.Create(
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

            await announcementRepository.AddAsync(announcement);

            return Result<Announcement>.Success(announcement);
        }
        catch (ArgumentException ex)
        {
            return Result<Announcement>.Failure(ex.Message);
        }
    }

    public async Task<Result<Announcement>> UpdateAsync(
        AnnouncementId publicId,
        string title,
        string content,
        AnnouncementTypeEnum type,
        DateTime? visibleFrom,
        DateTime? visibleUntil,
        bool isDismissible,
        int sortOrder)
    {
        var announcement = await announcementRepository.GetByPublicIdAsync(publicId);

        if (announcement is null)
            return Result<Announcement>.Failure($"Announcement '{publicId}' not found");

        try
        {
            var renderedContent = markupParser.ToHtml(content);
            announcement.Update(title, content, renderedContent, type, visibleFrom, visibleUntil, isDismissible, sortOrder);
            await announcementRepository.UpdateAsync(announcement);

            return Result<Announcement>.Success(announcement);
        }
        catch (ArgumentException ex)
        {
            return Result<Announcement>.Failure(ex.Message);
        }
    }

    public async Task<Result> DeleteAsync(AnnouncementId publicId)
    {
        var announcement = await announcementRepository.GetByPublicIdAsync(publicId);

        if (announcement is null)
            return Result.Failure($"Announcement '{publicId}' not found");

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

    public async Task<Result<Announcement>> GetByIdAsync(AnnouncementId publicId)
    {
        var announcement = await announcementRepository.GetByPublicIdAsync(publicId);

        if (announcement is null)
            return Result<Announcement>.Failure($"Announcement '{publicId}' not found");

        return Result<Announcement>.Success(announcement);
    }

    public async Task<IEnumerable<Announcement>> GetByScopeAsync(
        AnnouncementScopeEnum scope,
        string scopeEntityId) =>
        await announcementRepository.GetByScopeAsync(scope, scopeEntityId);

    public async Task<IEnumerable<Announcement>> GetActiveForCommunityAsync(CommunityId communityId) =>
        await announcementRepository.GetActiveForCommunityAsync(communityId);

    public async Task<IEnumerable<Announcement>> GetActiveForHubAsync(HubId hubId) =>
        await announcementRepository.GetActiveForHubAsync(hubId);

    public async Task<IEnumerable<Announcement>> GetActiveForSpaceAsync(SpaceId spaceId) =>
        await announcementRepository.GetActiveForSpaceAsync(spaceId);
}
