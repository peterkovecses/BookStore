namespace BookStore.UI.Common.Extensions;

public static class NavigationManagerExtensions
{
    public static bool TryGetQueryString<T>(this NavigationManager navigationManager, string key, out T? value)
    {
        var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);

        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue(key, out var valueFromQueryString))
        {
            if (typeof(T) == typeof(int) && int.TryParse(valueFromQueryString, out var resultInt))
            {
                value = (T)(object)resultInt;

                return true;
            }

            if (typeof(T) == typeof(string))
            {
                value = (T)(object)valueFromQueryString.ToString();

                return true;
            }

            if (typeof(T) == typeof(bool) && bool.TryParse(valueFromQueryString, out var resultBool))
            {
                value = (T)(object)resultBool;

                return true;
            }
        }

        value = default;
        
        return false;
    }

    public static string GetQueryStringWithUpdatedValue<T>(this NavigationManager navigationManager, string key, T value)
    {
        var stringValue = value?.ToString();
        object? finalValue = string.IsNullOrWhiteSpace(stringValue) ? null : stringValue;

        return navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { key, finalValue }
        });
    }
}
