namespace Snakk.Application.UseCases;

using Snakk.Application.Repositories;
using Snakk.Shared.Models;

public class SearchUseCase(ISearchRepository searchRepository) : UseCaseBase
{
    public Task<PagedResult<DiscussionSearchResultDto>> SearchDiscussionsAsync(
        string query,
        string? authorPublicId = null,
        string? spacePublicId = null,
        string? hubPublicId = null,
        int offset = 0,
        int pageSize = 20,
        string? userId = null) =>
        searchRepository.SearchDiscussionsAsync(query, authorPublicId, spacePublicId, hubPublicId, offset, pageSize, userId);

    public Task<PagedResult<PostSearchResultDto>> SearchPostsAsync(
        string query,
        string? authorPublicId = null,
        string? discussionPublicId = null,
        string? spacePublicId = null,
        int offset = 0,
        int pageSize = 20) =>
        searchRepository.SearchPostsAsync(query, authorPublicId, discussionPublicId, spacePublicId, offset, pageSize);
}
