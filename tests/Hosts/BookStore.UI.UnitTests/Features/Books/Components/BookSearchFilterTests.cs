using BookStore.UI.Features.Books.Components;
using Kovecses.Result;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BookStore.UI.UnitTests.Features.Books.Components;

public class BookSearchFilterTests
{
    [Fact]
    public async Task Authors_WithLongNames_RenderFullNamesInNonShrinkingScrollableRows()
    {
        const string longName = "The Culinary Institute of America";
        var authors = Enumerable.Range(1, 15)
            .Select(id => new AuthorDto { Id = id, Name = id == 1 ? longName : $"Author {id}" })
            .ToArray();
        var apiClient = Substitute.For<IBookStoreApiClient>();
        apiClient.GetSearchFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new BookSearchFiltersDto { Authors = authors }));

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(apiClient)
            .AddSingleton(Substitute.For<NavigationManager>())
            .AddSingleton<NotificationService>()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BookSearchFilter.Filter)] = new BookSearchParameters { AuthorIds = [1] }
            });
            var component = await renderer.RenderComponentAsync<BookSearchFilter>(parameters);

            return component.ToHtmlString();
        });

        html.Should().Contain("d-flex flex-column gap-2 overflow-auto ps-2");
        html.Should().Contain("max-height: 200px;");
        html.Should().Contain(longName);
        html.Should().Contain("Author 15");
        html.Split("class=\"form-check flex-shrink-0\"").Should().HaveCount(authors.Length + 1);
        html.Should().Contain("id=\"author-1\" checked");
        html.Should().Contain("for=\"author-1\"");
    }
}
