namespace BookStore.Common.Pagination;

public interface IPagination
{
    const int MinPageNumber = 1;
    const int MaxPageSize = 100;
    const int DefaultPageSize = 15;

    int PageNumber { get; }
    int PageSize { get; }

    static IPagination Create(int pageNumber, int pageSize) => new ImmutablePagination(pageNumber, pageSize);
    static IPagination Default => Create(MinPageNumber, DefaultPageSize);
    static IPagination NoPagination() => Create(MinPageNumber, MaxPageSize);

    private record ImmutablePagination(int PageNumber, int PageSize) : IPagination;
}
