namespace BookStore.UI.Common.Models;

public class SortingParameters : ISorting
{
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }

    public void ToggleOrder() => IsDescending = !IsDescending;
}
