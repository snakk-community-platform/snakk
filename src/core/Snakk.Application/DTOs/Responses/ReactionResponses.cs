namespace Snakk.Application.DTOs.Responses;

public record ToggleReactionResponse(bool Added);

public record GetReactionCountsResponse(int ThumbsUp, int Heart, int Eyes, int Crazy);

public record UserReactionResponse(string? Reaction);
