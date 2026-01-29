namespace Snakk.Api.Models;

public record CreateSpaceRequest(
    string HubId,
    string Name,
    string Slug,
    string? Description);
