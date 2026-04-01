namespace Snakk.Web.Models.Bff;

/// <summary>
/// BFF response for resolving an internal entity path to type, publicId, and name.
/// Used by the frontend to attach hover popups to entity-link elements.
/// </summary>
public record BffEntityResolveResponse
{
    public required string Type { get; init; }
    public required string PublicId { get; init; }
    public required string Name { get; init; }
}
