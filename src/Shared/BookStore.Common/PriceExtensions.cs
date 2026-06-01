namespace BookStore.Common;

public static class PriceExtensions
{
    private static readonly CultureInfo UsCulture = new("en-US");

    public static string ToCurrency(this decimal price) => price.ToString("C", UsCulture);
}
