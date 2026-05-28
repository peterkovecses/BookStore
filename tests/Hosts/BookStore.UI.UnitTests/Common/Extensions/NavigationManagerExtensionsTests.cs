namespace BookStore.UI.UnitTests.Common.Extensions;

public class NavigationManagerExtensionsTests
{
    private readonly MockNavigationManager _sut = new();

    [Fact]
    public void TryGetQueryString_WhenKeyExistsAndTypeIsString_ShouldReturnTrueAndValue()
    {
        // Arrange
        const string key = "searchTerm";
        const string expected = "test";
        _sut.SetUri($"http://localhost/books?{key}={expected}");

        // Act
        var result = _sut.TryGetQueryString<string>(key, out var actual);

        // Assert
        result.Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Fact]
    public void TryGetQueryString_WhenKeyExistsAndTypeIsInt_ShouldReturnTrueAndValue()
    {
        // Arrange
        const string key = "pageNumber";
        const int expected = 2;
        _sut.SetUri($"http://localhost/books?{key}={expected}");

        // Act
        var result = _sut.TryGetQueryString<int>(key, out var actual);

        // Assert
        result.Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Fact]
    public void TryGetQueryString_WhenKeyDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        _sut.SetUri("http://localhost/books");

        // Act
        var result = _sut.TryGetQueryString<string>("nonexistent", out var actual);

        // Assert
        result.Should().BeFalse();
        actual.Should().BeNull();
    }

    [Fact]
    public void GetQueryStringWithUpdatedValue_WhenValueIsProvided_ShouldUpdateQueryString()
    {
        // Arrange
        _sut.SetUri("http://localhost/books?searchTerm=old");
        const string key = "searchTerm";
        const string newValue = "new";

        // Act
        var actual = _sut.GetQueryStringWithUpdatedValue(key, newValue);

        // Assert
        actual.Should().Contain($"{key}={newValue}");
    }

    [Fact]
    public void GetQueryStringWithUpdatedValue_WhenValueIsNull_ShouldRemoveKeyFromQueryString()
    {
        // Arrange
        _sut.SetUri("http://localhost/books?searchTerm=old&page=1");
        const string key = "searchTerm";

        // Act
        var actual = _sut.GetQueryStringWithUpdatedValue<string?>(key, null);

        // Assert
        actual.Should().NotContain(key);
        actual.Should().Contain("page=1");
    }

    private class MockNavigationManager : NavigationManager
    {
        public MockNavigationManager() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Uri = uri;
            NotifyLocationChanged(false);
        }
        
        public void SetUri(string uri)
        {
            Uri = uri;
            NotifyLocationChanged(false);
        }
    }
}
