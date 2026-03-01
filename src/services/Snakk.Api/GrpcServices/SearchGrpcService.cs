using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Shared.Helpers;
using Snakk.Application.UseCases;
using Snakk.Protos;
using Snakk.Protos.Search;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class SearchGrpcService(
    SearchUseCase searchUseCase,
    Application.Repositories.ISearchRepository searchRepository) : SearchService.SearchServiceBase
{
    public override async Task<PagedDiscussionSearchResults> SearchDiscussions(SearchDiscussionsRequest request, ServerCallContext context)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await searchUseCase.SearchDiscussionsAsync(
            request.Query,
            request.HasAuthorId ? request.AuthorId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasHubId ? request.HubId : null,
            request.Offset,
            pageSize);

        var response = new PagedDiscussionSearchResults
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var d in result.Items)
        {
            var item = new DiscussionSearchResult
            {
                PublicId = d.PublicId,
                Title = d.Title,
                Slug = d.Slug,
                Highlight = d.Title, // Use title as highlight for now
                CreatedAt = ToTimestamp(d.CreatedAt),
                Space = new EntityRef
                {
                    PublicId = d.SpacePublicId,
                    Slug = d.SpaceSlug,
                    Name = d.SpaceName
                },
                Hub = new EntityRef
                {
                    PublicId = d.HubSlug, // No hub public ID in search results, use slug
                    Slug = d.HubSlug,
                    Name = d.HubSlug
                },
                Author = new AuthorRef
                {
                    PublicId = d.AuthorPublicId,
                    DisplayName = d.AuthorDisplayName,
                    AvatarUrl = AvatarHelper.GetAvatarUrl(d.AuthorPublicId, AvatarEntityType.User, 0)
                },
                PostCount = d.PostCount
            };

            if (d.LastActivityAt.HasValue)
                item.LastActivityAt = ToTimestamp(d.LastActivityAt.Value);

            response.Items.Add(item);
        }

        return response;
    }

    public override async Task<PagedPostSearchResults> SearchPosts(SearchPostsRequest request, ServerCallContext context)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await searchUseCase.SearchPostsAsync(
            request.Query,
            request.HasAuthorId ? request.AuthorId : null,
            request.HasDiscussionId ? request.DiscussionId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.Offset,
            pageSize);

        var response = new PagedPostSearchResults
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var p in result.Items)
        {
            response.Items.Add(new PostSearchResult
            {
                PublicId = p.PublicId,
                ContentHighlight = p.Content,
                CreatedAt = ToTimestamp(p.CreatedAt),
                Author = new AuthorRef
                {
                    PublicId = p.AuthorPublicId,
                    DisplayName = p.AuthorDisplayName,
                    AvatarUrl = AvatarHelper.GetAvatarUrl(p.AuthorPublicId, AvatarEntityType.User, 0)
                },
                DiscussionPublicId = p.DiscussionPublicId,
                DiscussionTitle = p.DiscussionTitle,
                DiscussionSlug = p.DiscussionSlug,
                Space = new EntityRef
                {
                    PublicId = p.SpaceSlug,
                    Slug = p.SpaceSlug,
                    Name = p.SpaceSlug
                },
                Hub = new EntityRef
                {
                    PublicId = p.HubSlug,
                    Slug = p.HubSlug,
                    Name = p.HubSlug
                }
            });
        }

        return response;
    }

    public override async Task<SitemapResponse> GetSitemapDiscussions(SitemapRequest request, ServerCallContext context)
    {
        var discussions = await searchRepository.GetSitemapDiscussionsAsync();

        var response = new SitemapResponse();

        foreach (var d in discussions)
        {
            response.Discussions.Add(new SitemapDiscussion
            {
                PublicId = d.PublicId,
                Slug = d.Slug,
                HubSlug = d.HubSlug,
                SpaceSlug = d.SpaceSlug,
                CommunitySlug = d.CommunitySlug,
                LastModified = ToTimestamp(d.LastModified),
                IsPinned = d.IsPinned
            });
        }

        return response;
    }

    private static Timestamp ToTimestamp(DateTime dt)
        => Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
