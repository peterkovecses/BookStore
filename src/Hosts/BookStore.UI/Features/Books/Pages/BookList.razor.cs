namespace BookStore.UI.Features.Books.Pages;

public partial class BookList
{
    private const int FilterDebounceDelayMilliseconds = 600;

    [Inject]
    private IBookStoreApiClient ApiClient { get; init; } = default!;

    protected override async Task LoadDataAsync()
    {
        Items = null;
        Loading = true;

        try
        {
            var result = await ApiClient.SearchBooksAsync(Filter, Pagination, Sorting, CancellationToken);

            if (!result.IsSuccess)
            {
                NotificationService.ShowFailure(result.FirstErrorMessage ?? "Failed to load books.");
                return;
            }

            Items = result.Data;
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        finally
        {
            Loading = false;
        }
    }

    protected override void SyncFilterWithUrl()
    {
        Filter.SearchTerm = NavigationManager.TryGetQueryParameter<string>("searchTerm", out var searchTerm) 
            ? searchTerm 
            : string.Empty;

        Filter.AuthorIds = NavigationManager.TryGetQueryParameters<int>("authorIds", out var authorIds) 
            ? [.. authorIds] 
            : [];

        Filter.TagIds = NavigationManager.TryGetQueryParameters<string>("tagIds", out var tagIds) 
            ? [.. tagIds] 
            : [];
    }

    protected override Dictionary<string, object?> GetFilterUrlParameters()
    {
        return new Dictionary<string, object?>
        {
            ["searchTerm"] = string.IsNullOrWhiteSpace(Filter.SearchTerm) ? null : Filter.SearchTerm,
            ["authorIds"] = Filter.AuthorIds.Count == 0 ? null : Filter.AuthorIds.ToArray(),
            ["tagIds"] = Filter.TagIds.Count == 0 ? null : Filter.TagIds.ToArray()
        };
    }

    private void OnPageChanged(int pageNumber)
    {
        Pagination.PageNumber = pageNumber;
        UpdateUrl();
    }

    private void OnPageSizeChanged(int pageSize)
    {
        Pagination.PageSize = pageSize;
        Pagination.PageNumber = 1;
        UpdateUrl();
    }

    private void OnFilterChanged()
    {
        Debounce(() => 
        {
            Pagination.PageNumber = 1;
            UpdateUrl();
        }, FilterDebounceDelayMilliseconds);
    }

    private void ToggleSortDirection()
    {
        Sorting.ToggleOrder();
        UpdateUrl();
    }
}
