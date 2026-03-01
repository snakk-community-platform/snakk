namespace Snakk.Application.DTOs.Responses;

public record ReadStateResponse(string? LastReadPostId, DateTime? LastReadAt);
