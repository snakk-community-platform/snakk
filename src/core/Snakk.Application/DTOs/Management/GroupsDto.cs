using System.ComponentModel.DataAnnotations;

namespace Snakk.Application.DTOs.Management;

public class GroupDto
{
    public string PublicId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public int SortOrder { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GroupListDto
{
    public List<GroupDto> Groups { get; set; } = new();
}

public class GroupMemberDto
{
    public string UserPublicId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
}

public class GroupMemberListDto
{
    public List<GroupMemberDto> Members { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CreateGroupRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Slug { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsPublic { get; set; } = true;
    public int SortOrder { get; set; }
}

public class UpdateGroupRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsPublic { get; set; }
    public int SortOrder { get; set; }
}

public class AddGroupMemberRequest
{
    [Required]
    public string UserPublicId { get; set; } = string.Empty;
}
