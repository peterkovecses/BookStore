namespace BookStore.Common.Pagination;

public interface IPagination
{
    int PageNumber { get; init; }
    int PageSize { get; init; }
}
