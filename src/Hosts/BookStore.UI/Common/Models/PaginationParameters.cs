namespace BookStore.UI.Common.Models;

public class PaginationParameters : IPagination
{
    public int PageNumber { get; set; } = IPagination.MinPageNumber;
    public int PageSize { get; set; } = IPagination.DefaultPageSize;

    public void Reset() => PageNumber = IPagination.MinPageNumber;
}
