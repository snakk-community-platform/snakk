namespace Snakk.Application.DTOs.Responses;

public record ToggleReactionResponse(bool Added);

public record GetReactionCountsResponse(Dictionary<string, int> Counts);

public record UserReactionsResponse(List<string> Reactions);
