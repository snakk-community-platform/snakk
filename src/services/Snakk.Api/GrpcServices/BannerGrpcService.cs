using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Protos.Banner;

namespace Snakk.Api.GrpcServices;

public class BannerGrpcService(
    BannerUseCase bannerUseCase) : BannerService.BannerServiceBase
{
    public override async Task<BannerList> GetActiveForCommunity(
        GetActiveBannersRequest request,
        ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var announcements = await bannerUseCase.GetActiveForCommunityAsync(
            CommunityId.From(request.EntityId));

        return ToBannerList(announcements);
    }

    public override async Task<BannerList> GetActiveForHub(
        GetActiveBannersRequest request,
        ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var announcements = await bannerUseCase.GetActiveForHubAsync(
            HubId.From(request.EntityId));

        return ToBannerList(announcements);
    }

    public override async Task<BannerList> GetActiveForSpace(
        GetActiveBannersRequest request,
        ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var announcements = await bannerUseCase.GetActiveForSpaceAsync(
            SpaceId.From(request.EntityId));

        return ToBannerList(announcements);
    }

    private static BannerList ToBannerList(IEnumerable<Banner> announcements)
    {
        var list = new BannerList();

        foreach (var a in announcements)
        {
            list.Banners.Add(new BannerInfo
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
