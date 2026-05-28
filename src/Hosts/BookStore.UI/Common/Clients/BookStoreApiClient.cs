namespace BookStore.UI.Common.Clients;

public class BookStoreApiClient(IHttpClientFactory httpClientFactory) : IBookStoreApiClient
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("bookstore-api");

    public async Task<Result<PaginatedList<BookSummaryDto>>> SearchBooksAsync(
        string? searchTerm,
        int[]? authorIds,
        string[]? tagIds,
        int pageNumber,
        int pageSize,
        string? sortBy,
        bool isDescending,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query.Add("searchTerm", searchTerm);
        }

        if (authorIds is { Length: > 0 })
        {
            foreach (var id in authorIds) query.Add("authorIds", id.ToString());
        }
        
        if (tagIds is { Length: > 0 })
        {
            foreach (var id in tagIds) query.Add("tagIds", id);
        }

        query.Add("pageNumber", pageNumber.ToString());
        query.Add("pageSize", pageSize.ToString());
        if (!string.IsNullOrWhiteSpace(sortBy)) query.Add("sortBy", sortBy);
        query.Add("isDescending", isDescending.ToString().ToLower());

        var uri = QueryHelpers.AddQueryString("books", query);

        return await _httpClient.GetAsync(uri, cancellationToken).ToResultAsync<PaginatedList<BookSummaryDto>>(cancellationToken);
    }

    public async Task<Result<BookDetailsDto>> GetBookDetailsAsync(int id, CancellationToken cancellationToken = default) 
        => await _httpClient.GetAsync($"books/{id}", cancellationToken).ToResultAsync<BookDetailsDto>(cancellationToken);

    public async Task<Result<BookSearchFiltersDto>> GetSearchFiltersAsync(CancellationToken cancellationToken = default) 
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
