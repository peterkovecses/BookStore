namespace BookStore.UI.Common.Models;

public class UIPagination : IPagination
{
    public int PageNumber { get; set; } = IPagination.MinPageNumber;
    public int PageSize { get; set; } = IPagination.DefaultPageSize;
}
