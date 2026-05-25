namespace BookStore.Common.Sorting;

public interface ISorting
{
    string? SortBy { get; }
    bool IsDescending { get; }

    static ISorting Create(string? sortBy, bool isDescending) => new ImmutableSorting(sortBy, isDescending);

    private record ImmutableSorting(string? SortBy, bool IsDescending) : ISorting;
}
