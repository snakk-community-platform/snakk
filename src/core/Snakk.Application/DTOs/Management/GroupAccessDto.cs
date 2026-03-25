namespace Snakk.Application.DTOs.Management;

public class GroupAccessGrantDto
{
    public required string GroupPublicId { get; init; }
    public required string GroupName { get; init; }
    public bool CanRead { get; init; }
    public bool CanWrite { get; init; }
}

public class EntityAccessDto
{
    public bool IsRestricted { get; init; }
    public List<GroupAccessGrantDto> Grants { get; init; } = [];
}

public class UpsertGroupGrantRequest
{
    public required string GroupPublicId { get; init; }
    public bool CanRead { get; init; }
    public bool CanWrite { get; init; }
}

public class SetIsRestrictedRequest
{
    public bool IsRestricted { get; init; }
}

public class GroupAccessResult
{
    public bool CanRead { get; init; }
    public bool CanWrite { get; init; }
    public bool IsRestricted { get; init; }

    public static GroupAccessResult Open => new() { CanRead = true, CanWrite = true, IsRestricted = false };
    public static GroupAccessResult Denied => new() { CanRead = false, CanWrite = false, IsRestricted = true };
}
