namespace BookStore.UI.Features.Shared.BaseComponents;

/// <summary>
/// Generic base class for list-based pages, providing pagination, sorting, and filtering logic.
/// </summary>
/// <typeparam name="TItem">The type of the items in the list.</typeparam>
/// <typeparam name="TFilter">The type of the filter object.</typeparam>
public abstract class ListComponentBase<TItem, TFilter> : CustomComponentBase
    where TFilter : class, new()
{
    private readonly Debouncer _debouncer = new();

    /// <summary>
    /// Gets or sets the filter object.
    /// </summary>
    protected TFilter Filter { get; set; } = new();

    /// <summary>
    /// Gets or sets the pagination state.
    /// </summary>
    protected PaginationParameters Pagination { get; set; } = new();

    /// <summary>
    /// Gets or sets the sorting state.
    /// </summary>
    protected SortingParameters Sorting { get; set; } = new();

    /// <summary>
    /// Gets or sets the paginated list of items.
    /// </summary>
    protected PaginatedList<TItem>? Items { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        NavigationManager.LocationChanged += HandleLocationChanged;
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        SyncStateWithUrl();
        await LoadDataAsync();
    }

    private async void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        // React to URL changes (e.g. back/forward button or NavigateTo)
        await InvokeAsync(async () => 
        {
            SyncStateWithUrl();
            await LoadDataAsync();
            StateHasChanged();
        });
    }

    /// <summary>
    /// Loads the data from the API.
    /// </summary>
    protected abstract Task LoadDataAsync();

    /// <summary>
    /// Synchronizes the component state with the current URL query parameters.
    /// </summary>
    protected virtual void SyncStateWithUrl()
    {
        Pagination.PageNumber = NavigationManager.TryGetQueryParameter<int>("page", out var page) && page > 0 
            ? page 
            : IPagination.MinPageNumber;

        Pagination.PageSize = NavigationManager.TryGetQueryParameter<int>("pageSize", out var pageSize) && pageSize > 0 
            ? pageSize 
            : IPagination.DefaultPageSize;

        Sorting.SortBy = NavigationManager.TryGetQueryParameter<string>("sortBy", out var sortBy) 
            ? sortBy 
            : null;

        Sorting.IsDescending = NavigationManager.TryGetQueryParameter<bool>("desc", out var desc) && desc;

        SyncFilterWithUrl();
    }

    /// <summary>
    /// Overridable method to synchronize the filter object with the URL query parameters.
    /// </summary>
    protected virtual void SyncFilterWithUrl() { }

    /// <summary>
    /// Updates the URL query parameters based on the current component state and navigates.
    /// </summary>
    protected void UpdateUrl()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["page"] = Pagination.PageNumber == IPagination.MinPageNumber ? null : Pagination.PageNumber,
            ["pageSize"] = Pagination.PageSize == IPagination.DefaultPageSize ? null : Pagination.PageSize,
            ["sortBy"] = Sorting.SortBy,
            ["desc"] = Sorting.IsDescending ? true : (bool?)null
        };

        foreach (var (key, value) in GetFilterUrlParameters())
        {
            parameters[key] = value;
        }

        var uri = NavigationManager.GetUriWithQueryParameters(parameters);
        NavigationManager.NavigateTo(uri);
    }

    /// <summary>
    /// Overridable method to get the filter-specific URL query parameters.
    /// </summary>
    protected virtual Dictionary<string, object?> GetFilterUrlParameters() => [];

    /// <summary>
    /// Executes an action after a specified debounce period.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="milliseconds">The debounce period in milliseconds.</param>
    protected void Debounce(Action action, int milliseconds = 300)
    {
        _debouncer.Debounce(() => InvokeAsync(action), milliseconds);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        NavigationManager.LocationChanged -= HandleLocationChanged;
        _debouncer.Dispose();
        base.Dispose();
    }
}
