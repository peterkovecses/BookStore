namespace BookStore.UI.Features.Books.Pages;

public partial class BookDetails
{
    [Parameter]
    public int Id { get; set; }

    [Inject]
    private IBookStoreApiClient ApiClient { get; init; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; init; } = default!;

    private BookDetailsDto? _book;

    private async Task ScrollToReviews()
    {
        await JSRuntime.InvokeVoidAsync("scrollToElement", "reviews");
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _book = null;
        Loading = true;
        try
        {
            var result = await ApiClient.GetBookDetailsAsync(Id, CancellationToken);

            if (!result.IsSuccess)
            {
                NotificationService.ShowFailure(result.FirstErrorMessage ?? "Failed to load book details.");
                return;
            }

            _book = result.Data;
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
}
