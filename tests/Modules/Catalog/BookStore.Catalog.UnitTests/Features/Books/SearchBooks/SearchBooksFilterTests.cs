#pragma warning disable xUnit1016

namespace BookStore.Catalog.UnitTests.Features.Books.SearchBooks;

public class SearchBooksFilterTests
{
    [Fact]
    public void GetPredicate_WhenNoFiltersSet_ShouldReturnTruePredicate()
    {
        // Arrange
        var book = new Book { Id = 1, Title = "Test Book" };
        var sut = new SearchBooksFilter();

        // Act
        var predicate = sut.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(SearchTermTestData))]
    internal void GetPredicate_WhenSearchTermMatches_ShouldMatch(SearchBooksFilter filter, Book book)
    {
        // Arrange & Act
        var predicate = filter.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeTrue();
    }

    internal static TheoryData<SearchBooksFilter, Book> SearchTermTestData()
    {
        var data = new TheoryData<SearchBooksFilter, Book>();

        // Title
        data.Add(
            new SearchBooksFilter { SearchTerm = "Test" },
            new Book { Id = 1, Title = "Test Book", Description = "", Publisher = "", AuthorsLink = [] }
        );

        // Description
        data.Add(
            new SearchBooksFilter { SearchTerm = "Fiction" },
            new Book { Id = 1, Title = "Book", Description = "Great Fiction", Publisher = "", AuthorsLink = [] }
        );

        // Publisher
        data.Add(
            new SearchBooksFilter { SearchTerm = "Publisher" },
            new Book { Id = 1, Title = "", Description = "", Publisher = "Test Publisher", AuthorsLink = [] }
        );

        // AuthorName
        data.Add(
            new SearchBooksFilter { SearchTerm = "Smith" },
            new Book 
            { 
                Id = 1, Title = "", Description = "", Publisher = "", 
                AuthorsLink = [new BookAuthor { Author = new Author { Id = 1, Name = "John Smith" } }]
            }
        );

        return data;
    }

    [Fact]
    public void GetPredicate_WhenSearchTermNotMatching_ShouldNotMatch()
    {
        // Arrange
        var sut = new SearchBooksFilter { SearchTerm = "NonExistent" };
        var book = new Book { Id = 1, Title = "Test", Description = "Description", Publisher = "Publisher", AuthorsLink = [] };

        // Act
        var predicate = sut.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void GetPredicate_WhenAuthorIdMatches_ShouldMatch()
    {
        // Arrange
        var sut = new SearchBooksFilter { AuthorIds = [1, 2] };
        var book = new Book 
        { 
            Id = 1, Title = "", Description = "", Publisher = "", 
            AuthorsLink = [new BookAuthor { AuthorId = 1 }]
        };

        // Act
        var predicate = sut.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void GetPredicate_WhenAuthorIdNotMatching_ShouldNotMatch()
    {
        // Arrange
        var sut = new SearchBooksFilter { AuthorIds = [5, 6] };
        var book = new Book 
        { 
            Id = 1, Title = "", Description = "", Publisher = "", 
            AuthorsLink = [new BookAuthor { AuthorId = 1 }]
        };

        // Act
        var predicate = sut.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void GetPredicate_WhenTagIdMatches_ShouldMatch()
    {
        // Arrange
        var sut = new SearchBooksFilter { TagIds = ["Fiction", "Adventure"] };
        var book = new Book 
        { 
            Id = 1, Title = "", Description = "", Publisher = "", AuthorsLink = [],
            Tags = [new Tag { Id = "Fiction" }]
        };

        // Act
        var predicate = sut.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void GetPredicate_WhenTagIdNotMatching_ShouldNotMatch()
    {
        // Arrange
        var sut = new SearchBooksFilter { TagIds = ["Horror"] };
        var book = new Book 
        { 
            Id = 1, Title = "", Description = "", Publisher = "", AuthorsLink = [],
            Tags = [new Tag { Id = "Fiction" }]
        };

        // Act
        var predicate = sut.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void GetPredicate_WhenMultipleFiltersApplied_ShouldMatchAll()
    {
        // Arrange
        var sut = new SearchBooksFilter
        {
            SearchTerm = "Test",
            AuthorIds = [1],
            TagIds = ["Fiction"]
        };

        var author = new Author { Id = 1, Name = "Author" };
        var book = new Book 
        { 
            Id = 1, Title = "Test Book", Description = "", Publisher = "", 
            AuthorsLink = [new BookAuthor { AuthorId = 1, Author = author }],
            Tags = [new Tag { Id = "Fiction" }]
        };

        // Act
        var predicate = sut.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void GetPredicate_WhenBookIsSoftDeleted_ShouldNotMatch()
    {
        // Arrange
        var sut = new SearchBooksFilter();
        var book = new Book { Id = 1, SoftDeleted = true };

        // Act
        var predicate = sut.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void GetPredicate_WhenAuthorIdsIsEmpty_ShouldNotFilterByAuthor()
    {
        // Arrange
        var sut = new SearchBooksFilter { AuthorIds = [] };
        var book = new Book 
        { 
            Id = 1, Title = "Test Book", Description = "", Publisher = "", 
            AuthorsLink = [new BookAuthor { AuthorId = 1 }]
        };

        // Act
        var predicate = sut.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void GetPredicate_WhenTagIdsIsEmpty_ShouldNotFilterByTag()
    {
        // Arrange
        var sut = new SearchBooksFilter { TagIds = [] };
        var book = new Book 
        { 
            Id = 1, Title = "Test Book", Description = "", Publisher = "", AuthorsLink = [],
            Tags = [new Tag { Id = "Fiction" }]
        };

        // Act
        var predicate = sut.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetPredicate_WhenSearchTermIsEmptyOrWhitespace_ShouldNotApplySearchFilter(string? searchTerm)
    {
        // Arrange
        var sut = new SearchBooksFilter { SearchTerm = searchTerm };
        var book = new Book { Id = 1, Title = "Test Book", Description = "", Publisher = "", AuthorsLink = [] };

        // Act
        var predicate = sut.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void GetPredicate_WhenMultipleFiltersAppliedAndOneDoesNotMatch_ShouldReturnFalse()
    {
        // Arrange
        var sut = new SearchBooksFilter
        {
            SearchTerm = "Test",
            AuthorIds = [1],
            TagIds = ["Fiction"]
        };

        var author = new Author { Id = 1, Name = "Author" };
        var book = new Book 
        { 
            Id = 1, Title = "Test Book", Description = "", Publisher = "", 
            AuthorsLink = [new BookAuthor { AuthorId = 1, Author = author }],
            Tags = [new Tag { Id = "NonMatchingTag" }]
        };

        // Act
        var predicate = sut.GetPredicate();
        var compiledPredicate = predicate.Compile();
        var actual = compiledPredicate(book);

        // Assert
        actual.Should().BeFalse();
    }
}
