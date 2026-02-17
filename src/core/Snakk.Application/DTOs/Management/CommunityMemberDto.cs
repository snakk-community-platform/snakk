namespace Snakk.Application.DTOs.Management;

public class CommunityMemberDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime JoinedAt { get; set; }
    public List<string> Roles { get; set; } = new(); // "Owner", "Admin", "Moderator", "Member"
    public int PostCount { get; set; }
    public int DiscussionCount { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public bool IsBanned { get; set; }
}

public class CommunityMembersListDto
{
    public List<CommunityMemberDto> Members { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class UpdateMemberRoleRequest
{
    public string Action { get; set; } = string.Empty; // "add" or "remove"
    public string Role { get; set; } = string.Empty; // "Admin", "Moderator"
}
