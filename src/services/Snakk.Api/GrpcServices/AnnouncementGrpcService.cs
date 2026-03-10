using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Protos.Announcement;

namespace Snakk.Api.GrpcServices;

public class AnnouncementGrpcService(
    AnnouncementUseCase announcementUseCase) : AnnouncementService.AnnouncementServiceBase
{
    public override async Task<AnnouncementList> GetActiveForCommunity(
        GetActiveAnnouncementsRequest request,
        ServerCallContext context)
    {
        var announcements = await announcementUseCase.GetActiveForCommunityAsync(
            CommunityId.From(request.EntityId));

        return ToAnnouncementList(announcements);
    }

    public override async Task<AnnouncementList> GetActiveForHub(
        GetActiveAnnouncementsRequest request,
        ServerCallContext context)
    {
        var announcements = await announcementUseCase.GetActiveForHubAsync(
            HubId.From(request.EntityId));

        return ToAnnouncementList(announcements);
    }

    public override async Task<AnnouncementList> GetActiveForSpace(
        GetActiveAnnouncementsRequest request,
        ServerCallContext context)
    {
        var announcements = await announcementUseCase.GetActiveForSpaceAsync(
            SpaceId.From(request.EntityId));

        return ToAnnouncementList(announcements);
    }

    private static AnnouncementList ToAnnouncementList(IEnumerable<Announcement> announcements)
    {
        var list = new AnnouncementList();

        foreach (var a in announcements)
        {
            list.Announcements.Add(new AnnouncementInfo
            {
                PublicId = a.PublicId.Value,
                Title = a.Title,
                RenderedContent = a.RenderedContent,
                Type = a.Type.ToString(),
                Scope = a.Scope.ToString(),
                IsDismissible = a.IsDismissible,
                SortOrder = a.SortOrder,
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc))
            });
        }

        return list;
    }
}
