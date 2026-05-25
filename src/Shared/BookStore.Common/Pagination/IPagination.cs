namespace BookStore.Common.Pagination;

public interface IPagination
{
    const int MinPageNumber = 1;
    const int MaxPageSize = 5000;
    const int DefaultPageSize = 15;

    int PageNumber { get; }
    int PageSize { get; }
}
