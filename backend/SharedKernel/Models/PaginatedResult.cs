namespace SharedKernel.Models;

public class PaginatedResult<T>
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalRecords { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
    public IEnumerable<T> Data { get; init; } = [];

    public static PaginatedResult<T> Create(IEnumerable<T> data, int totalRecords, int page, int pageSize)
        => new() { Data = data, TotalRecords = totalRecords, Page = page, PageSize = pageSize };
}
