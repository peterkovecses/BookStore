namespace BookStore.Common.Pagination;

public record PaginationParams(int PageNumber, int PageSize) : IPagination;
