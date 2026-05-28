namespace BookStore.UI.Common.Clients;

public class BookStoreApiClient(IHttpClientFactory httpClientFactory) : IBookStoreApiClient
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("bookstore-api");

    public async Task<Result<PaginatedList<BookSummaryDto>>> SearchBooksAsync(
        BookSearchParameters searchParameters,
        IPagination pagination,
        ISorting sorting,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>();

        if (!string.IsNullOrWhiteSpace(searchParameters.SearchTerm))
        {
            query.Add("searchTerm", searchParameters.SearchTerm);
        }

        if (searchParameters.AuthorIds.Count > 0)
        {
            foreach (var id in searchParameters.AuthorIds) query.Add("authorIds", id.ToString());
        }

        if (searchParameters.TagIds.Count > 0)
        {
            foreach (var id in searchParameters.TagIds) query.Add("tagIds", id);
        }

        query.Add("pageNumber", pagination.PageNumber.ToString());
        query.Add("pageSize", pagination.PageSize.ToString());
        if (!string.IsNullOrWhiteSpace(sorting.SortBy)) query.Add("sortBy", sorting.SortBy);
        query.Add("isDescending", sorting.IsDescending.ToString().ToLower());

        var uri = QueryHelpers.AddQueryString("books", query);

        return await _httpClient.GetAsync(uri, cancellationToken).ToResultAsync<PaginatedList<BookSummaryDto>>(cancellationToken);
    }


    public async Task<Result<BookDetailsDto>> GetBookDetailsAsync(int id, CancellationToken cancellationToken) 
        => await _httpClient.GetAsync($"books/{id}", cancellationToken).ToResultAsync<BookDetailsDto>(cancellationToken);

    public async Task<Result<BookSearchFiltersDto>> GetSearchFiltersAsync(CancellationToken cancellationToken) 
        => await _httpClient.GetAsync("books/filters", cancellationToken).ToResultAsync<BookSearchFiltersDto>(cancellationToken);
}

public static class HttpResponseMessageExtensions
{
    public static async Task<Result<T>> ToResultAsync<T>(this Task<HttpResponseMessage> responseTask, CancellationToken cancellationToken)
    {
        try
        {
            var response = await responseTask;

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<T>(cancellationToken);

                return data is not null ? Result.Success(data) : Result.Failure<T>(UIErrors.EmptyResponse());
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var validationProblem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
                if (validationProblem?.Errors is not null)
                {
                    var metadata = validationProblem.Errors.ToDictionary(
                        kvp => kvp.Key,
                        kvp => (object)kvp.Value);

                    return Result.Failure<T>(UIErrors.Validation(metadata));
                }
            }

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
            var errorMessage = problem?.Detail ?? problem?.Title ?? $"API call failed with status code {response.StatusCode}";
            
            return Result.Failure<T>(UIErrors.ApiError(errorMessage));
        }
        catch (Exception ex)
        {
            return Result.Failure<T>(UIErrors.Exception(ex.Message));
        }
    }
}
