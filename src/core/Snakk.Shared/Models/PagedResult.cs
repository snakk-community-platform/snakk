namespace Snakk.Shared.Models;

public record PagedResult<T>
{
    public IEnumerable<T> Items { get; init; } = [];
    public int Offset { get; init; }
    public int PageSize { get; init; }
    public bool HasMoreItems { get; init; }
    
    /// <summary>
    /// Cursor for keyset pagination. Encode as base64 of "sortValue~id".
    /// Null if no more items.
    /// </summary>
    public string? NextCursor { get; init; }
}
