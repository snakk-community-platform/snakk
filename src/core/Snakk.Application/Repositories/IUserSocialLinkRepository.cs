namespace Snakk.Application.Repositories;

public interface IUserSocialLinkRepository
{
    Task<List<(string Platform, string Username)>> GetByUserPublicIdAsync(string publicId);
    Task<List<(string Platform, string Username)>> GetByUserInternalIdAsync(int userId);
    Task ReplaceAllAsync(int userId, List<(string Platform, string Username)> links);
    Task<int?> GetUserInternalIdByPublicIdAsync(string publicId);
}
