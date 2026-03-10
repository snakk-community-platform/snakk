namespace Snakk.Api.Models;

public record CreateAnnouncementRequest(
    string Title,
    string Content,
    string Type,
    string Scope,
    string ScopeEntityId,
    DateTime? VisibleFrom,
    DateTime? VisibleUntil,
    bool IsDismissible = true,
    int SortOrder = 0);

public record UpdateAnnouncementRequest(
    string Title,
    string Content,
    string Type,
    DateTime? VisibleFrom,
    DateTime? VisibleUntil,
    bool IsDismissible = true,
    int SortOrder = 0);
