namespace Snakk.Api.Models;

public record CreateBannerRequest(
    string Title,
    string Content,
    string Type,
    string Scope,
    string ScopeEntityId,
    DateTime? VisibleFrom,
    DateTime? VisibleUntil,
    bool IsDismissible = true,
    int SortOrder = 0);

public record UpdateBannerRequest(
    string Title,
    string Content,
    string Type,
    DateTime? VisibleFrom,
    DateTime? VisibleUntil,
    bool IsDismissible = true,
    int SortOrder = 0);
