namespace Snakk.Application.Repositories;

public interface IUserSocialLinkRepository
{
    Task<List<(string Platform, string Username)>> GetByUserPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<List<(string Platform, string Username)>> GetByUserInternalIdAsync(int userId, CancellationToken ct = default);
    Task ReplaceAllAsync(int userId, List<(string Platform, string Username)> links, CancellationToken ct = default);
    Task<int?> GetUserInternalIdByPublicIdAsync(string publicId, CancellationToken ct = default);
}
