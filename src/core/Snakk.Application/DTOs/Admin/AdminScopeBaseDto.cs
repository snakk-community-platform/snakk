namespace Snakk.Application.DTOs.Admin;

public abstract record AdminScopeBaseDto
{
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
}
