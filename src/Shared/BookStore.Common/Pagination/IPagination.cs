namespace BookStore.Common.Pagination;

public interface IPagination
{
    public const int MinPageNumber = 1;
    public const int MaxPageSize = 100;

    int PageNumber { get; init; }
    int PageSize { get; init; }
}
