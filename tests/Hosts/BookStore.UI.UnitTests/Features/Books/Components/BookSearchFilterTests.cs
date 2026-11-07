namespace BookStore.UI.UnitTests.Features.Books.Components;

public class BookSearchFilterTests
{
    [Fact]
    public async Task Render_WhenAuthorsIncludeLongName_ShouldRenderFullNamesAndSelectedAuthor()
    {
        // Arrange
        const string expectedLongName = "The Culinary Institute of America";
        const int selectedAuthorId = 1;
        const int authorCount = 15;
        var expectedAuthors = Enumerable.Range(selectedAuthorId, authorCount)
            .Select(id => new AuthorDto { Id = id, Name = id == selectedAuthorId ? expectedLongName : $"Author {id}" })
            .ToArray();
        var apiClient = Substitute.For<IBookStoreApiClient>();
        apiClient.GetSearchFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new BookSearchFiltersDto { Authors = expectedAuthors }));

        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(apiClient)
            .AddSingleton(Substitute.For<NavigationManager>())
            .AddSingleton<NotificationService>()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(BookSearchFilter.Filter)] = new BookSearchParameters { AuthorIds = [selectedAuthorId] }
        });

        // Act
        var actual = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var sut = await renderer.RenderComponentAsync<BookSearchFilter>(parameters);

            return sut.ToHtmlString();
        });

        // Assert
        foreach (var expected in expectedAuthors)
        {
            actual.Should().Contain(expected.Name);
        }

        actual.Should().Contain($"id=\"author-{selectedAuthorId}\" checked");
        actual.Should().Contain($"for=\"author-{selectedAuthorId}\"");
    }
}
