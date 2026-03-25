using Snakk.Application.DTOs.Management;

namespace Snakk.Application.Services;

public interface IGroupService
{
    // Group CRUD
    Task<GroupListDto> GetGroupsAsync(string communityId, CancellationToken ct = default);

    Task<GroupDto?> GetGroupAsync(string communityId, string groupId, CancellationToken ct = default);

    Task<GroupDto> CreateGroupAsync(string communityId, CreateGroupRequest request, string actingUserPublicId, CancellationToken ct = default);

    Task<GroupDto?> UpdateGroupAsync(string communityId, string groupId, UpdateGroupRequest request, CancellationToken ct = default);

    Task<bool> DeleteGroupAsync(string communityId, string groupId, CancellationToken ct = default);

    // Member management
    Task<GroupMemberListDto> GetMembersAsync(string communityId, string groupId, int page = 1, int pageSize = 50, CancellationToken ct = default);

    Task<bool> AddMemberAsync(string communityId, string groupId, AddGroupMemberRequest request, string actingUserPublicId, CancellationToken ct = default);

    Task<bool> RemoveMemberAsync(string communityId, string groupId, string userPublicId, CancellationToken ct = default);
}
