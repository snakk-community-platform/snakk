namespace Snakk.Application.DTOs.Responses;

public record UserSearchResult(string PublicId, string DisplayName, string AvatarUrl);

public record TopContributorResponse(
    string PublicId,
    string DisplayName,
    string AvatarUrl,
    int PostCountToday);

public record TopContributorsResponse(IEnumerable<TopContributorResponse> Items);

public record ActivityHistoryResponse(
    int Days,
    IEnumerable<ActivityDayResponse> Data);

public record ActivityDayResponse(
    string Date,
    int Discussions,
    int Posts,
    int Total);
