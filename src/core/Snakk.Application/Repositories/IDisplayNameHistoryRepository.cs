namespace Snakk.Application.Repositories;

public interface IDisplayNameHistoryRepository
{
    Task AddAsync(string userPublicId, string previousName, string newName, string? changedByUserPublicId = null, CancellationToken ct = default);
    Task<bool> WasNameEverUsedAsync(string displayName, string? excludeUserPublicId = null, CancellationToken ct = default);
    Task<List<DisplayNameHistoryDto>> GetHistoryForUserAsync(string userPublicId, int limit = 20, CancellationToken ct = default);
}

public record DisplayNameHistoryDto(
    string PreviousName,
    string NewName,
    DateTime ChangedAt,
    string? ChangedByUserPublicId);
