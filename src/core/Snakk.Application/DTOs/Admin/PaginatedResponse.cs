namespace Snakk.Application.DTOs.Admin;

public class PaginatedResponse<T>
{
    public required List<T> Items { get; set; }
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
