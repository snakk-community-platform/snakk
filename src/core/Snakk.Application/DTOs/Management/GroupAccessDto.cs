namespace Snakk.Application.DTOs.Management;

using Snakk.Shared.Enums;

public class GroupAccessGrantDto
{
    public required string GroupPublicId { get; init; }
    public required string GroupName { get; init; }
    public AccessLevelEnum AccessLevel { get; init; }
}

public class EntityAccessDto
{
    public bool IsRestricted { get; init; }
    public List<GroupAccessGrantDto> Grants { get; init; } = [];
}

public class UpsertGroupGrantRequest
{
    public required string GroupPublicId { get; init; }
    public AccessLevelEnum AccessLevel { get; init; }
}

public class SetIsRestrictedRequest
{
    public bool IsRestricted { get; init; }
}

public class GroupAccessResult
{
    public AccessLevelEnum AccessLevel { get; init; }
    public bool IsRestricted { get; init; }

    public bool CanRead => AccessLevel >= AccessLevelEnum.Read;
    public bool CanAuthor => AccessLevel >= AccessLevelEnum.Author;
    public bool CanWrite => AccessLevel >= AccessLevelEnum.Write;

    public static GroupAccessResult Open => new() { AccessLevel = AccessLevelEnum.Write, IsRestricted = false };
    public static GroupAccessResult Denied => new() { AccessLevel = AccessLevelEnum.None, IsRestricted = true };
}
