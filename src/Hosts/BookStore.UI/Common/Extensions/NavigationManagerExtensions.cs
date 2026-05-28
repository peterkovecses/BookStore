namespace BookStore.UI.Common.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="NavigationManager"/> class.
/// </summary>
public static class NavigationManagerExtensions
{
    /// <summary>
    /// Tries to get a query parameter value from the current URI.
    /// </summary>
    /// <typeparam name="T">The type of the parameter value.</typeparam>
    /// <param name="navigationManager">The navigation manager.</param>
    /// <param name="key">The query parameter key.</param>
    /// <param name="value">When this method returns, contains the parameter value if the key was found and successfully converted; otherwise, the default value for the type.</param>
    /// <returns><c>true</c> if the query parameter was found and successfully converted; otherwise, <c>false</c>.</returns>
    public static bool TryGetQueryParameter<T>(this NavigationManager navigationManager, string key, out T? value)
    {
        var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);

        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue(key, out var valueFromQueryString))
        {
            var rawValue = valueFromQueryString.FirstOrDefault();

            if (rawValue is not null)
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));

                if (converter.CanConvertFrom(typeof(string)) && converter.IsValid(rawValue))
                {
                    try
                    {
                        value = (T?)converter.ConvertFromString(rawValue);

                        return true;
                    }
                    catch
                    {
                        // Fallback to default if conversion fails despite IsValid check
                    }
                }
            }
        }

        value = default;

        return false;
    }

    /// <summary>
    /// Returns a new URI with the specified query parameter updated or added.
    /// </summary>
    /// <typeparam name="T">The type of the parameter value.</typeparam>
    /// <param name="navigationManager">The navigation manager.</param>
    /// <param name="key">The query parameter key.</param>
    /// <param name="value">The new value for the query parameter. If <c>null</c> or whitespace (for strings), the parameter will be removed.</param>
    /// <returns>A new URI string with the updated query parameter.</returns>
    public static string GetUriWithUpdatedQueryParameter<T>(this NavigationManager navigationManager, string key, T value)
    {
        object? finalValue = value;

        if (value is string stringValue && string.IsNullOrWhiteSpace(stringValue))
        {
            finalValue = null;
        }

        return navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { key, finalValue }
        });
    }
}