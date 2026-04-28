namespace BookStore.Common.Sorting;

public interface ISorting
{
    string? SortBy { get; init; }
    bool IsDescending { get; init; }
}
