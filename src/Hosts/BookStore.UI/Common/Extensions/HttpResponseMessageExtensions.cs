namespace BookStore.UI.Common.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="HttpResponseMessage"/> class.
/// </summary>
public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Converts an <see cref="HttpResponseMessage"/> to a <see cref="Result{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <param name="response">The HTTP response message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Result{T}"/> indicating the outcome of the API call.</returns>
    public static async Task<Result<T>> ToResultAsync<T>(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<T>(cancellationToken);

                return data is not null ? Result.Success(data) : Result.Failure<T>(UIErrors.EmptyResponse());
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                try
                {
                    var validationProblem = JsonSerializer.Deserialize<ValidationProblemDetails>(content, JsonSerializerOptions.Web);

                    if (validationProblem?.Errors is not null)
                    {
                        var metadata = validationProblem.Errors.ToDictionary(
                            kvp => kvp.Key,
                            kvp => (object)kvp.Value);

                        return Result.Failure<T>(UIErrors.Validation(metadata));
                    }
                }
                catch
                {
                    // Fallback to generic error if validation problem parsing fails
                }

                string? errorMessage = null;

                try
                {
                    var problem = JsonSerializer.Deserialize<ProblemDetails>(content, JsonSerializerOptions.Web);
                    errorMessage = problem?.Detail ?? problem?.Title;
                }
                catch
                {
                    // Fallback to status code message if problem details parsing fails
                }

                errorMessage ??= $"API call failed with status code {response.StatusCode}";

                return Result.Failure<T>(UIErrors.ApiError(errorMessage));
            }

            string? genericErrorMessage = null;

            try
            {
                var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
                genericErrorMessage = problem?.Detail ?? problem?.Title;
            }
            catch
            {
                // Fallback to status code message if problem details parsing fails
            }

            genericErrorMessage ??= $"API call failed with status code {response.StatusCode}";

            return Result.Failure<T>(UIErrors.ApiError(genericErrorMessage));
        }
        catch (Exception ex)
        {
            return Result.Failure<T>(UIErrors.Exception(ex.Message));
        }
    }
}
