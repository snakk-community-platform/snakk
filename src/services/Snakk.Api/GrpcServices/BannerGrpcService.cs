using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Protos.Banner;

namespace Snakk.Api.GrpcServices;

public class BannerGrpcService(
    BannerUseCase bannerUseCase,
    HybridCache cache) : BannerService.BannerServiceBase
{
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromMinutes(5) };

    public override Task<BannerList> GetActiveForCommunity(
        GetActiveBannersRequest request, ServerCallContext context) =>
        cache.GetOrCreateAsync(
            $"banners:community:{request.EntityId}",
            async ct => ToBannerList(await bannerUseCase.GetActiveForCommunityAsync(CommunityId.From(request.EntityId))),
            CacheOptions,
            cancellationToken: context.CancellationToken).AsTask();

    public override Task<BannerList> GetActiveForHub(
        GetActiveBannersRequest request, ServerCallContext context) =>
        cache.GetOrCreateAsync(
            $"banners:hub:{request.EntityId}",
            async ct => ToBannerList(await bannerUseCase.GetActiveForHubAsync(HubId.From(request.EntityId))),
            CacheOptions,
            cancellationToken: context.CancellationToken).AsTask();

    public override Task<BannerList> GetActiveForSpace(
        GetActiveBannersRequest request, ServerCallContext context) =>
        cache.GetOrCreateAsync(
            $"banners:space:{request.EntityId}",
            async ct => ToBannerList(await bannerUseCase.GetActiveForSpaceAsync(SpaceId.From(request.EntityId))),
            CacheOptions,
            cancellationToken: context.CancellationToken).AsTask();

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
