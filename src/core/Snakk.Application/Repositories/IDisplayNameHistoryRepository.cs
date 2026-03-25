namespace Snakk.Application.Repositories;

public interface IDisplayNameHistoryRepository
{
    Task AddAsync(string userPublicId, string previousName, string newName, string? changedByUserPublicId = null);
    Task<bool> WasNameEverUsedAsync(string displayName);
    Task<List<DisplayNameHistoryDto>> GetHistoryForUserAsync(string userPublicId, int limit = 20);
}

public record DisplayNameHistoryDto(
    string PreviousName,
    string NewName,
    DateTime ChangedAt,
    string? ChangedByUserPublicId);
