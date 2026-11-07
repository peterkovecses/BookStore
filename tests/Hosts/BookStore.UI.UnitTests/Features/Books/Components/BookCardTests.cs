using System.Text.RegularExpressions;
using BookStore.UI.Features.Books.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BookStore.UI.UnitTests.Features.Books.Components;

public class BookCardTests
{
    [Theory]
    [InlineData(1, "First book", false)]
    [InlineData(42, "Another book", true)]
    public async Task BookCard_ShouldRenderOneNativeDetailsLink(int id, string title, bool hasPromotion)
    {
        // Arrange
        var book = new BookSummaryDto
        {
            Id = id,
            Title = title,
            Authors = [],
            Tags = [],
            Promotion = hasPromotion ? new PriceOfferDto { Text = "Sale", NewPrice = 5 } : null
        };
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(BookCard.Book)] = book });

        // Act
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<BookCard>(parameters);

            return component.ToHtmlString();
        });

        // Assert
        var links = Regex.Matches(html, "<a\\b[^>]*>.*?</a>", RegexOptions.Singleline);
        links.Should().ContainSingle();
        var link = links[0].Value;
        link.Should().Contain($"href=\"/books/{id}\"");
        link.Should().Contain("stretched-link");
        link.Should().Contain($"aria-label=\"View details for {title}\"");
        link.Should().Contain("Details");
        link.Should().NotContain("onclick");
        html.Should().NotContain("<button");
    }
}
