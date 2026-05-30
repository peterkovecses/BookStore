namespace BookStore.UI.Common.Models;

public class UISorting : ISorting
{
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
}
