namespace Snakk.Application.DTOs.Responses;

public record PagedResponse<T>(
    IEnumerable<T> Items,
    int Offset,
    int PageSize,
    bool HasMoreItems,
    string? NextCursor = null);
