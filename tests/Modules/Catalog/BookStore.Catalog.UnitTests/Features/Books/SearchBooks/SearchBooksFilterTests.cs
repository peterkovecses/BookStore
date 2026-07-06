
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

    [Fact]
    public void GetPredicate_WhenSearchTermInTitle_ShouldMatch()
    {
        // Arrange
        var sut = new SearchBooksFilter { SearchTerm = "Test" };
        var book = new Book { Id = 1, Title = "Test Book", Description = "", Publisher = "", AuthorsLink = [] };

        // Act
        var predicate = sut.GetPredicate();
        var actual = predicate.Compile()(book);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void GetPredicate_WhenSearchTermInDescription_ShouldMatch()
    {
        // Arrange
        var sut = new SearchBooksFilter { SearchTerm = "Fiction" };
        var book = new Book { Id = 1, Title = "Book", Description = "Great Fiction", Publisher = "", AuthorsLink = [] };

        // Act
        var predicate = sut.GetPredicate();
        var actual = predicate.Compile()(book);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void GetPredicate_WhenSearchTermInPublisher_ShouldMatch()
    {
        // Arrange
        var sut = new SearchBooksFilter { SearchTerm = "Publisher" };
        var book = new Book { Id = 1, Title = "", Description = "", Publisher = "Test Publisher", AuthorsLink = [] };

        // Act
        var predicate = sut.GetPredicate();
        var actual = predicate.Compile()(book);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void GetPredicate_WhenSearchTermInAuthorName_ShouldMatch()
    {
        // Arrange
        var sut = new SearchBooksFilter { SearchTerm = "Smith" };
        var book = new Book
        {
            Id = 1, Title = "", Description = "", Publisher = "",
            AuthorsLink = [new BookAuthor { Author = new Author { Id = 1, Name = "John Smith" } }]
        };

        // Act
        var predicate = sut.GetPredicate();
        var actual = predicate.Compile()(book);

        // Assert
        actual.Should().BeTrue();
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
