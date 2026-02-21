namespace Snakk.Web.Models.Bff;

/// <summary>
/// BFF-specific auth status response.
/// Decouples frontend from internal API DTOs.
/// </summary>
public record BffAuthStatusResponse
{
    public required bool IsAuthenticated { get; init; }
    public string? PublicId { get; init; }
    public string? DisplayName { get; init; }
    public bool? EmailVerified { get; init; }
    public string? Role { get; init; }
    public string? AvatarUrl { get; init; }
}
