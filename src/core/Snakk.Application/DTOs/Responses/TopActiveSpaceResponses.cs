namespace Snakk.Application.DTOs.Responses;

public record TopActiveSpaceResponse(
    string PublicId,
    string Name,
    string Slug,
    int PostCountToday,
    EntityRef Hub);

public record TopActiveSpacesResponse(IEnumerable<TopActiveSpaceResponse> Items);
