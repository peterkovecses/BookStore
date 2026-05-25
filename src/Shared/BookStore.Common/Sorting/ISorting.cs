namespace BookStore.Common.Sorting;

public interface ISorting
{
    string? SortBy { get; }
    bool IsDescending { get; }
}
