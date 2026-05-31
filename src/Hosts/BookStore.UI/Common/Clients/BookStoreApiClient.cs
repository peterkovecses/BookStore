using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;

namespace BookStore.UI.Common.Clients;

public class BookStoreApiClient(
    HttpClient httpClient, 
    IJSRuntime jsRuntime, 
    IConfiguration configuration) : IBookStoreApiClient
{
    private async Task EnsureBaseAddressSet()
    {
        if (httpClient.BaseAddress is not null)
        {
            return;
        }

        // Try to get the URL from the index.html placeholder (via JS)
        var apiUrl = await jsRuntime.InvokeAsync<string>("getApiUrl");

        if (string.IsNullOrEmpty(apiUrl) || apiUrl.StartsWith("{{"))
        {
            // Fallback to configuration or default localhost
            apiUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7068";
        }

        httpClient.BaseAddress = new Uri(apiUrl.EndsWith('/') ? apiUrl : $"{apiUrl}/");
    }

    public async Task<Result<PaginatedList<BookSummaryDto>>> SearchBooksAsync(
        BookSearchParameters searchParameters,
        IPagination pagination,
        ISorting sorting,
        CancellationToken cancellationToken)
    {
        await EnsureBaseAddressSet();

        var query = new List<KeyValuePair<string, string?>>();

        if (!string.IsNullOrWhiteSpace(searchParameters.SearchTerm))
        {
            query.Add(new("searchTerm", searchParameters.SearchTerm));
        }

        if (searchParameters.AuthorIds.Count > 0)
        {
            query.AddRange(searchParameters.AuthorIds.Select(id => new KeyValuePair<string, string?>("authorIds", id.ToString())));
        }

        if (searchParameters.TagIds.Count > 0)
        {
            query.AddRange(searchParameters.TagIds.Select(id => new KeyValuePair<string, string?>("tagIds", id)));
        }

        query.Add(new("pageNumber", pagination.PageNumber.ToString()));
        query.Add(new("pageSize", pagination.PageSize.ToString()));

        if (!string.IsNullOrWhiteSpace(sorting.SortBy))
        {
            query.Add(new("sortBy", sorting.SortBy));
        }

        query.Add(new("isDescending", sorting.IsDescending.ToString().ToLower()));

        var uri = QueryHelpers.AddQueryString("api/catalog/books", query);

        using var response = await httpClient.GetAsync(uri, cancellationToken);

        return await response.ToResultAsync<PaginatedList<BookSummaryDto>>(cancellationToken);
    }

    public async Task<Result<BookDetailsDto>> GetBookDetailsAsync(int id, CancellationToken cancellationToken)
    {
        await EnsureBaseAddressSet();

        using var response = await httpClient.GetAsync($"api/catalog/books/{id}", cancellationToken);

        return await response.ToResultAsync<BookDetailsDto>(cancellationToken);
    }

    public async Task<Result<BookSearchFiltersDto>> GetSearchFiltersAsync(CancellationToken cancellationToken)
    {
        await EnsureBaseAddressSet();

        using var response = await httpClient.GetAsync("api/catalog/books/filters", cancellationToken);

        return await response.ToResultAsync<BookSearchFiltersDto>(cancellationToken);
    }
}
