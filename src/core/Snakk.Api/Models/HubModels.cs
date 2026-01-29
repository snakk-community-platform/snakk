namespace Snakk.Api.Models;

public record CreateHubRequest(
    string CommunityId,
    string Name,
    string Slug,
    string? Description);
