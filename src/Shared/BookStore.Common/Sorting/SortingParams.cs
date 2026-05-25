namespace BookStore.Common.Sorting;

public record SortingParams(string? SortBy, bool IsDescending) : ISorting;
